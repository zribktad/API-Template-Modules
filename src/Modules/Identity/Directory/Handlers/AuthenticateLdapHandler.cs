using Ardalis.Specification;
using BuildingBlocks.Domain.Interfaces;
using ErrorOr;
using Identity.Directory.Entities;
using Identity.Directory.Interfaces;
using Identity.Directory.Options;
using Identity.Persistence;
using Identity.ValueObjects;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Queries.Identity;

namespace Identity.Directory.Handlers;

/// <summary>
///     Specification to find a user by username in a specific tenant, ignoring global query filters.
/// </summary>
public sealed class UserByUsernameAndTenantSpecification : Specification<AppUser>
{
    public UserByUsernameAndTenantSpecification(string username, Guid tenantId)
    {
        string normalizedUsername = NormalizedString.Normalize(username);
        Query
            .IgnoreQueryFilters()
            .Where(u => u.DbNormalizedUsername == normalizedUsername && u.TenantId == tenantId);
    }
}

/// <summary>
///     Wolverine handler for AuthenticateLdapQuery.
/// </summary>
public static class AuthenticateLdapHandler
{
    public static async Task<ErrorOr.ErrorOr<LdapUserContract>> HandleAsync(
        AuthenticateLdapQuery query,
        ILdapService ldapService,
        IUserRepository userRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IOptions<LdapOptions> ldapOptions,
        CancellationToken ct
    )
    {
        ErrorOr.ErrorOr<LdapUserResponse> authResult = await ldapService.AuthenticateAsync(
            query.Username,
            query.Password,
            ct
        );

        if (authResult.IsError)
        {
            return authResult.Errors;
        }

        LdapUserResponse user = authResult.Value;
        Guid bootstrapTenantId = ldapOptions.Value.DefaultTenantId;

        // Provision local user if needed
        AppUser? localUser = await userRepository.FirstOrDefaultAsync(
            new UserByUsernameAndTenantSpecification(user.Username, bootstrapTenantId),
            ct
        );

        if (localUser is null)
        {
            localUser = AppUser.Create(
                user.Username,
                user.Email ?? $"{user.Username}@{ldapOptions.Value.FallbackEmailDomain}",
                keycloakUserId: null,
                tenantId: bootstrapTenantId
            );

            await userRepository.AddAsync(localUser, ct);
        }
        else if (!localUser.IsActive)
        {
            return Error.Validation(
                code: "Ldap.UserDisabled",
                description: "The authenticated LDAP user is disabled in the local system."
            );
        }

        // Update local details (Sync)
        if (!string.IsNullOrEmpty(user.Email))
        {
            localUser.Email = new NormalizedString(user.Email);
        }

        await unitOfWork.CommitAsync(ct);

        return new LdapUserContract(
            localUser.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.DistinguishedName
        );
    }
}
