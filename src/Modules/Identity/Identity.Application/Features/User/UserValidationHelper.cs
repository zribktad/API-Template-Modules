using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using SharedKernel.Application.Errors;
using ErrorOr;

namespace Identity.Application.Features.User;

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
        var normalized = AppUser.NormalizeUsername(username);
        if (await repository.ExistsByUsernameAsync(normalized, ct))
            return DomainErrors.Users.UsernameAlreadyExists(username);

        return Result.Success;
    }
}
