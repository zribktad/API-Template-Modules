using APITemplate.Domain.Entities;

namespace APITemplate.Infrastructure.Persistence.EntityNormalization;

/// <summary>
/// Normalizes <see cref="AppUser"/> fields (username and email) to their canonical
/// casing before the entity is persisted, enabling case-insensitive uniqueness checks.
/// </summary>
public sealed class AppUserEntityNormalizationService : IEntityNormalizationService
{
    /// <summary>
    /// Applies normalization to <paramref name="entity"/> when it is an <see cref="AppUser"/>;
    /// no-ops for all other entity types.
    /// </summary>
    public void Normalize(IAuditableTenantEntity entity)
    {
        if (entity is not AppUser user)
            return;

        user.NormalizedUsername = AppUser.NormalizeUsername(user.Username);
        user.NormalizedEmail = AppUser.NormalizeEmail(user.Email);
    }
}
