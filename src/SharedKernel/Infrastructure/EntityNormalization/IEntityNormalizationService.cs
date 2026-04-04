using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.EntityNormalization;

/// <summary>
///     Defines normalization behavior applied to auditable tenant entities before they are persisted.
/// </summary>
public interface IEntityNormalizationService
{
    /// <summary>Normalizes the relevant fields of <paramref name="entity" /> in place before persistence.</summary>
    public void Normalize(IAuditableTenantEntity entity);
}
