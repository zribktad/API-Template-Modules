using ErrorOr;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.Directory.Interfaces;
using Identity.Errors;
using Wolverine;

namespace Identity.Directory.Features.Account;

public sealed record ChangeOwnPasswordCommand(
    string KeycloakUserId,
    string PreferredUsername,
    ChangePasswordRequest Request
);

public sealed class ChangeOwnPasswordCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        ChangeOwnPasswordCommand command,
        IUserRepository repository,
        IKeycloakAdminService keycloakAdmin,
        IKeycloakAndBffGlobalLogoutService globalLogout,
        CancellationToken ct
    )
    {
        AppUser? user = await repository.FirstOrDefaultAsync(
            new UserByKeycloakUserIdSpecification(command.KeycloakUserId),
            ct
        );

        if (user is null)
            return DomainErrors.Users.NotFoundByKeycloakUserId(command.KeycloakUserId);

        if (
            string.Equals(
                command.Request.CurrentPassword,
                command.Request.NewPassword,
                StringComparison.Ordinal
            )
        )
            return DomainErrors.Users.NewPasswordMustDiffer();

        bool valid = await keycloakAdmin.ValidateCredentialsAsync(
            command.PreferredUsername,
            command.Request.CurrentPassword,
            ct
        );

        if (!valid)
            return DomainErrors.Users.CurrentPasswordInvalid();

        await keycloakAdmin.SetUserPasswordAsync(
            command.KeycloakUserId,
            command.Request.NewPassword,
            temporary: false,
            ct
        );

        await globalLogout.SignOutAfterCredentialChangeAsync(command.KeycloakUserId, ct);

        return Result.Success;
    }
}
