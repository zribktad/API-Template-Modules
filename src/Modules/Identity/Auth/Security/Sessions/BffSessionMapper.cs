using Identity.Auth.Entities;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Maps between <see cref="BffSessionRecord" /> and <see cref="BffPersistedSession" /> entity.
/// </summary>
internal static class BffSessionMapper
{
    public static BffPersistedSession ToEntity(
        BffSessionRecord session,
        BffSessionRecord protectedRecord
    )
    {
        BffPersistedSession entity = new()
        {
            Id = Guid.NewGuid(),
            SessionId = session.SessionId,
            UserId = session.UserId,
            Subject = session.Subject,
            EncryptedAccessToken = protectedRecord.AccessToken,
            EncryptedRefreshToken = protectedRecord.RefreshToken,
        };

        PopulateEntity(entity, session, protectedRecord);
        return entity;
    }

    public static void ApplyToEntity(
        BffPersistedSession entity,
        BffSessionRecord session,
        BffSessionRecord protectedRecord
    )
    {
        PopulateEntity(entity, session, protectedRecord);
    }

    public static BffSessionRecord ToRecord(BffPersistedSession entity)
    {
        return new BffSessionRecord
        {
            SessionId = entity.SessionId,
            UserId = entity.UserId,
            Subject = entity.Subject,
            Provider = entity.Provider,
            TenantId = entity.TenantId == Guid.Empty ? null : entity.TenantId.ToString(),
            Roles = entity.Roles,
            Email = entity.Email,
            DisplayName = entity.DisplayName,
            AccessToken = entity.EncryptedAccessToken,
            RefreshToken = entity.EncryptedRefreshToken,
            IdToken = entity.EncryptedIdToken,
            AccessTokenExpiresAtUtc = entity.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = entity.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastSeenAtUtc = entity.LastSeenAtUtc,
            LastRefreshedAtUtc = entity.LastRefreshedAtUtc,
            Status = entity.Status,
            Version = entity.Version,
            RevokedAtUtc = entity.RevokedAtUtc,
            RevocationReason = entity.RevocationReason,
        };
    }

    private static void PopulateEntity(
        BffPersistedSession entity,
        BffSessionRecord session,
        BffSessionRecord protectedRecord
    )
    {
        entity.UserId = session.UserId;
        entity.Subject = session.Subject;
        entity.Provider = session.Provider;
        entity.TenantId = ParseTenantId(session.TenantId);
        entity.Roles = session.Roles;
        entity.Email = session.Email;
        entity.DisplayName = session.DisplayName;
        entity.EncryptedAccessToken = protectedRecord.AccessToken;
        entity.EncryptedRefreshToken = protectedRecord.RefreshToken;
        entity.EncryptedIdToken = protectedRecord.IdToken;
        entity.AccessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc;
        entity.RefreshTokenExpiresAtUtc = session.RefreshTokenExpiresAtUtc;
        entity.CreatedAtUtc = session.CreatedAtUtc;
        entity.LastSeenAtUtc = session.LastSeenAtUtc;
        entity.LastRefreshedAtUtc = session.LastRefreshedAtUtc;
        entity.Status = session.Status;
        entity.Version = session.Version;
        entity.RevokedAtUtc = session.RevokedAtUtc;
        entity.RevocationReason = session.RevocationReason;
    }

    private static Guid ParseTenantId(string? tenantId)
    {
        return Guid.TryParse(tenantId, out Guid parsed) ? parsed : Guid.Empty;
    }
}
