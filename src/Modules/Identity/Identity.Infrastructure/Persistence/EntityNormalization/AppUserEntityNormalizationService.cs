using SharedKernel.Domain.Entities.Contracts;

namespace Identity.Infrastructure.Persistence.EntityNormalization;

/// <summary>
/// Normalizes <see cref="AppUser"/> fields (username and email) to their canonical
/// casing before the entity is persisted, enabling case-insensitive uniqueness checks.
/// </summary>
public sealed class AppUserEntityNormalizationService : IEntityNormalizationService
{
    public void Normalize(IAuditableTenantEntity entity)
    {
        if (entity is not AppUser user)
            return;

        user.NormalizedUsername = AppUser.NormalizeUsername(user.Username);
        user.NormalizedEmail = user.Email.Normalize();
    }
}
