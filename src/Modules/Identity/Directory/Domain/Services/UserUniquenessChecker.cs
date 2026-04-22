using ErrorOr;
using Identity.Directory.Interfaces;

namespace Identity.Directory.Domain.Services;

/// <summary>
///     Default <see cref="IUserUniquenessChecker" /> backed by <see cref="IUserRepository" /> existence queries.
///     Keeps uniqueness conflict error mapping out of command handlers and out of the repository.
/// </summary>
internal sealed class UserUniquenessChecker(IUserRepository repository) : IUserUniquenessChecker
{
    public async Task<ErrorOr<Success>> EnsureEmailUniqueAsync(
        string email,
        CancellationToken ct = default
    )
    {
        if (await repository.ExistsByEmailAsync(email, ct))
            return DomainErrors.Users.EmailAlreadyExists(email);

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> EnsureUsernameUniqueAsync(
        string username,
        CancellationToken ct = default
    )
    {
        string normalized = NormalizedString.Normalize(username);
        if (await repository.ExistsByUsernameAsync(normalized, ct))
            return DomainErrors.Users.UsernameAlreadyExists(username);

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> EnsureUniqueAsync(
        string username,
        string email,
        CancellationToken ct = default
    )
    {
        ErrorOr<Success> emailResult = await EnsureEmailUniqueAsync(email, ct);
        if (emailResult.IsError)
            return emailResult.Errors;

        return await EnsureUsernameUniqueAsync(username, ct);
    }
}
