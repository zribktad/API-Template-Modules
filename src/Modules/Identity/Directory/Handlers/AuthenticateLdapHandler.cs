using BuildingBlocks.Domain.Interfaces;
using ErrorOr;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Interfaces;
using SharedKernel.Contracts.Queries.Identity;

namespace Identity.Directory.Handlers;

/// <summary>
///     Wolverine handler for AuthenticateLdapQuery.
/// </summary>
public static class AuthenticateLdapHandler
{
    public static async Task<ErrorOr.ErrorOr<LdapUserContract>> HandleAsync(
        AuthenticateLdapQuery query,
        ILdapService ldapService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
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

        // 4. Provision local user if needed
        AppUser? localUser = await userRepository.FirstOrDefaultAsync(
            new UserByUsernameSpecification(user.Username),
            ct
        );

        if (localUser is null)
        {
            localUser = AppUser.Create(
                user.Username,
                user.Email ?? $"{user.Username}@ldap.local", // Fallback email
                keycloakUserId: null,
                tenantId: Guid.Parse(AuthConstants.Tenants.Bootstrap) // LDAP users belong to bootstrap tenant initially
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

        // 5. Update local details (Sync)
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
