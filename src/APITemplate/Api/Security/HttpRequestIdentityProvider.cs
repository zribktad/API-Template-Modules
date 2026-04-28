using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Auth.Security;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Security;

/// <summary>
///     Single per-request source for <see cref="ICurrentRequestUser" />, <see cref="IActorProvider" />,
///     and <see cref="ITenantProvider" /> derived from <see cref="Microsoft.AspNetCore.Http.HttpContext.User" />.
///     All claims are parsed once on first access and cached in <see cref="IdentitySnapshot" />.
/// </summary>
public sealed class HttpRequestIdentityProvider
    : ICurrentRequestUser,
        IActorProvider,
        ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private IdentitySnapshot? _snapshot;

    public HttpRequestIdentityProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? OidcSubject => GetSnapshot().OidcSubject;
    public Guid? ApplicationUserId => GetSnapshot().ApplicationUserId;
    public string? PreferredUsername => GetSnapshot().PreferredUsername;
    public bool IsInteractiveUser => GetSnapshot().IsInteractiveUser;
    public Guid ActorId => GetSnapshot().ActorId;
    public Guid TenantId => GetSnapshot().TenantId;
    public bool HasTenant => GetSnapshot().TenantId != Guid.Empty;

    private IdentitySnapshot GetSnapshot() =>
        _snapshot ??= Compute(_httpContextAccessor.HttpContext?.User);

    private static IdentitySnapshot Compute(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return new IdentitySnapshot();

        string? nameId =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.NameId);

        string? oidcSubject =
            user.FindFirstValue(AuthConstants.Claims.Subject)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        string? preferredUsername =
            user.FindFirstValue(AuthConstants.Claims.PreferredUsername)
            ?? user.FindFirstValue(ClaimTypes.Name);

        bool isInteractiveUser = !KeycloakServiceAccountClaims.IsServiceAccount(user);

        Guid? applicationUserId = null;
        if (
            Guid.TryParse(nameId, out Guid nameIdGuid)
            && (
                oidcSubject is null || !string.Equals(nameId, oidcSubject, StringComparison.Ordinal)
            )
        )
            applicationUserId = nameIdGuid;

        // NameIdentifier → Subject → Name (no Jwt sub fallback — matches legacy HttpActorProvider).
        string? actorRaw = nameId ?? oidcSubject ?? user.FindFirstValue(ClaimTypes.Name);
        Guid actorId = Guid.TryParse(actorRaw, out Guid parsed) ? parsed : Guid.Empty;

        Guid tenantId = Guid.TryParse(
            user.FindFirstValue(AuthConstants.Claims.TenantId),
            out Guid tid
        )
            ? tid
            : Guid.Empty;

        return new IdentitySnapshot(
            oidcSubject,
            applicationUserId,
            preferredUsername,
            isInteractiveUser,
            actorId,
            tenantId
        );
    }

    private readonly record struct IdentitySnapshot(
        string? OidcSubject = null,
        Guid? ApplicationUserId = null,
        string? PreferredUsername = null,
        bool IsInteractiveUser = true,
        Guid ActorId = default,
        Guid TenantId = default
    );
}
