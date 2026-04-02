using APITemplate.Application.Common.Errors;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.User;

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
