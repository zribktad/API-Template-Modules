using ErrorOr;
using Identity.Directory.Interfaces;
using Identity.ValueObjects;

namespace Identity.Directory.Domain.Services;

/// <summary>
///     Default <see cref="ITenantUniquenessChecker" /> backed by <see cref="ITenantRepository" /> existence queries.
///     Keeps uniqueness conflict error mapping out of command handlers and out of the repository.
/// </summary>
internal sealed class TenantUniquenessChecker(ITenantRepository repository) : ITenantUniquenessChecker
{
    public async Task<ErrorOr<Success>> EnsureCodeUniqueAsync(
        TenantCode code,
        CancellationToken ct = default
    )
    {
        if (await repository.ExistsByCodeAsync(code.Value, ct))
            return DomainErrors.Tenants.CodeAlreadyExists(code.Value);

        return Result.Success;
    }
}
