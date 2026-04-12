using ErrorOr;

namespace Identity.Directory.Features.User;

internal static class UserValidationHelper
{
    internal static async Task<ErrorOr<Success>> ValidateEmailUniqueAsync(
        IUserRepository repository,
        string email,
        CancellationToken ct
    )
    {
        if (await repository.ExistsByEmailAsync(email, ct))
            return DomainErrors.Users.EmailAlreadyExists(email);

        return Result.Success;
    }

    internal static async Task<ErrorOr<Success>> ValidateUsernameUniqueAsync(
        IUserRepository repository,
        string username,
        CancellationToken ct
    )
    {
        string normalized = AppUser.NormalizeUsername(username);
        if (await repository.ExistsByUsernameAsync(normalized, ct))
            return DomainErrors.Users.UsernameAlreadyExists(username);

        return Result.Success;
    }
}
