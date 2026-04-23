using ErrorOr;

namespace Identity.Directory.Domain.Services;

/// <summary>
///     Enforces uniqueness invariants for <see cref="Identity.Directory.Entities.Tenant" /> code.
///     Repositories expose raw existence queries (<c>ExistsBy*Async</c>); this service adds the business rule layer
///     — mapping conflicts to <see cref="DomainErrors.Tenants" /> error codes.
/// </summary>
public interface ITenantUniquenessChecker
{
    /// <summary>
    ///     Returns an error if a tenant with the given code already exists.
    /// </summary>
    Task<ErrorOr<Success>> EnsureCodeUniqueAsync(string code, CancellationToken ct = default);
}
