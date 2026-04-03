using Identity.Entities;
using SharedKernel.Domain.Interfaces;

namespace Identity.Interfaces;

/// <summary>
/// Repository contract for <see cref="Tenant"/> entities.
/// </summary>
public interface ITenantRepository : IRepository<Tenant>;

