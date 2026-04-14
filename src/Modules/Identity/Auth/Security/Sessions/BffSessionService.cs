using System.Diagnostics;
using Identity.Auth.Security.Sessions.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Owns the lifecycle of persisted BFF sessions, including creation from authentication
///     tickets, validation on load, mutation, and revocation.
/// </summary>
public sealed class BffSessionService : IBffSessionService, IBffSessionRevocationService
{
    private readonly IBffSessionMutator _mutator;
    private readonly IBffSessionPrincipalFactory _principalFactory;
    private readonly IBffSessionRecordFactory _recordFactory;
    private readonly IBffSessionStore _sessionStore;
    private readonly IBffSessionValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BffSessionService(
        IBffSessionStore sessionStore,
        IBffSessionPrincipalFactory principalFactory,
        IBffSessionRecordFactory recordFactory,
        IBffSessionValidator validator,
        IBffSessionMutator mutator,
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _sessionStore = sessionStore;
        _principalFactory = principalFactory;
        _recordFactory = recordFactory;
        _validator = validator;
        _mutator = mutator;
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(
        AuthenticationTicket ticket,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        BffSessionRecord session = _recordFactory.CreateNew(ticket, now);
        await _sessionStore.StoreAsync(session, ct);
        return session.SessionId;
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> GetTicketAsync(
        string sessionId,
        CancellationToken ct = default
    )
    {
        BffSessionRecord? session = await GetSessionAsync(sessionId, ct);
        if (session is null)
            return null;

        return _principalFactory.CreateTicket(session, AuthConstants.BffSchemes.Cookie);
    }

    /// <inheritdoc />
    public async Task<BffSessionRecord?> GetSessionAsync(
        string sessionId,
        CancellationToken ct = default
    )
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        string cacheKey = BffRequestScopedSessionCache.GetItemKey(sessionId);
        if (
            httpContext is not null
            && httpContext.Items.TryGetValue(cacheKey, out object? cached)
            && cached is BffSessionRecord cachedSession
        )
            return cachedSession;

        BffSessionRecord? session = await _sessionStore.GetAsync(sessionId, ct);
        if (session is null)
            return null;

        BffSessionRecord? validated = await ValidateLoadedSessionAsync(session, ct);
        if (validated is null)
            return null;

        if (httpContext is not null)
            httpContext.Items[cacheKey] = validated;

        return validated;
    }

    /// <inheritdoc />
    public async Task UpdateSessionFromTicketAsync(
        string sessionId,
        AuthenticationTicket ticket,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        await _mutator.MutateAsync(
            sessionId,
            currentSession => _recordFactory.CreateUpdated(sessionId, ticket, currentSession, now),
            ct
        );
    }

    /// <inheritdoc />
    public Task RevokeAsync(
        string sessionId,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return _mutator.MutateAsync(
            sessionId,
            currentSession =>
                currentSession with
                {
                    Status = BffSessionStatus.Revoked,
                    RevokedAtUtc = now,
                    RevocationReason = reason,
                    LastSeenAtUtc = now,
                    Version = currentSession.Version + 1,
                },
            ct
        );
    }

    /// <inheritdoc />
    public async Task RevokeAllSessionsForSubjectAsync(
        string keycloakSubject,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<string> revokedIds =
            await _sessionStore.BulkRevokeActiveSessionsBySubjectAsync(
                keycloakSubject,
                reason,
                now,
                ct
            );

        foreach (string sessionId in revokedIds)
            BffRequestScopedSessionCache.Invalidate(_httpContextAccessor, sessionId);
    }

    private Task ExpireAsync(string sessionId, CancellationToken ct)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return _mutator.MutateAsync(
            sessionId,
            currentSession =>
                currentSession with
                {
                    Status = BffSessionStatus.Expired,
                    LastSeenAtUtc = now,
                    Version = currentSession.Version + 1,
                },
            ct
        );
    }

    private async Task<BffSessionRecord?> ValidateLoadedSessionAsync(
        BffSessionRecord session,
        CancellationToken ct
    )
    {
        BffSessionValidationResult validation = _validator.Validate(
            session,
            _timeProvider.GetUtcNow()
        );

        switch (validation.Action)
        {
            case BffSessionValidationAction.Accept:
                return session;
            case BffSessionValidationAction.Reject:
                return null;
            case BffSessionValidationAction.Expire:
                await ExpireAsync(session.SessionId, ct);
                return null;
            case BffSessionValidationAction.Revoke:
                await RevokeAsync(
                    session.SessionId,
                    validation.RevocationReason ?? BffSessionRevocationReason.SessionCorrupted,
                    ct
                );
                return null;
            default:
                throw new UnreachableException();
        }
    }
}
