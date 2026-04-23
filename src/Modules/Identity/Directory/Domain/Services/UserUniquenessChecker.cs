using ErrorOr;
using Identity.Directory.Interfaces;

namespace Identity.Directory.Domain.Services;

/// <summary>
///     Translates raw <see cref="IUserRepository"/> existence queries into domain-level uniqueness errors.
///     Centralising this mapping here keeps command handlers free of error-code knowledge and the
///     repository free of business rules.
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
        if (await repository.ExistsByUsernameAsync(username, ct))
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
