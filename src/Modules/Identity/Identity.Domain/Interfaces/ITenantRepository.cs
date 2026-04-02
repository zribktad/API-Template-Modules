using Identity.Domain.Entities;
using SharedKernel.Domain.Interfaces;

namespace Identity.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="Tenant"/> entities.
/// </summary>
public interface ITenantRepository : IRepository<Tenant>;
