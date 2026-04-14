using Identity.Auth.Options;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions.Lifecycle;

internal sealed class BffSessionValidator : IBffSessionValidator
{
    private readonly BffOptions _options;

    public BffSessionValidator(IOptions<BffOptions> options)
    {
        _options = options.Value;
    }

    public BffSessionValidationResult Validate(BffSessionRecord session, DateTimeOffset now)
    {
        if (
            session.Status == BffSessionStatus.Revoked
            || session.Status == BffSessionStatus.Expired
        )
            return BffSessionValidationResult.Reject();

        if (IsMalformed(session))
            return BffSessionValidationResult.Revoke(BffSessionRevocationReason.SessionCorrupted);

        if (HasRefreshTokenExpired(session, now))
            return BffSessionValidationResult.Expire();

        if (HasExceededAbsoluteLifetime(session, now))
        {
            return BffSessionValidationResult.Revoke(
                BffSessionRevocationReason.AbsoluteLifetimeExceeded
            );
        }

        return BffSessionValidationResult.Accept();
    }

    private bool HasExceededAbsoluteLifetime(BffSessionRecord session, DateTimeOffset now)
    {
        DateTimeOffset absoluteExpiry = session.CreatedAtUtc.AddMinutes(
            _options.SessionAbsoluteLifetimeMinutes
        );
        return now >= absoluteExpiry;
    }

    private static bool HasRefreshTokenExpired(BffSessionRecord session, DateTimeOffset now)
    {
        return session.RefreshTokenExpiresAtUtc.HasValue
            && now >= session.RefreshTokenExpiresAtUtc.Value;
    }

    private static bool IsMalformed(BffSessionRecord session)
    {
        return string.IsNullOrWhiteSpace(session.SessionId)
            || string.IsNullOrWhiteSpace(session.UserId)
            || string.IsNullOrWhiteSpace(session.Subject)
            || string.IsNullOrWhiteSpace(session.AccessToken);
    }
}
