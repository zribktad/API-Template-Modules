using APITemplate.Domain.Entities;

namespace APITemplate.Infrastructure.Persistence.EntityNormalization;

/// <summary>
/// Defines normalization behavior applied to <see cref="IAuditableTenantEntity"/> instances
/// before they are persisted, such as lowercasing lookup fields.
/// </summary>
public interface IEntityNormalizationService
{
    /// <summary>Normalizes the relevant fields of <paramref name="entity"/> in place before persistence.</summary>
    void Normalize(IAuditableTenantEntity entity);
}
