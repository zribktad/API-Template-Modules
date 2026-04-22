using ErrorOr;

namespace Identity.Directory.Domain.Services;

/// <summary>
///     Checks whether an email or username is already taken and returns a typed domain error if so.
///     Callers pass raw (un-normalised) values — normalisation is the responsibility of the implementation.
/// </summary>
public interface IUserUniquenessChecker
{
    /// <summary>
    ///     Returns <see cref="DomainErrors.Users.EmailAlreadyExists"/> if the email is taken (case-insensitive).
    /// </summary>
    Task<ErrorOr<Success>> EnsureEmailUniqueAsync(string email, CancellationToken ct = default);

    /// <summary>
    ///     Returns <see cref="DomainErrors.Users.UsernameAlreadyExists"/> if the username is taken (case-insensitive).
    /// </summary>
    Task<ErrorOr<Success>> EnsureUsernameUniqueAsync(
        string username,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Checks email then username, short-circuiting on the first conflict.
    ///     Use during registration where both must be unique before the user is created.
    /// </summary>
    Task<ErrorOr<Success>> EnsureUniqueAsync(
        string username,
        string email,
        CancellationToken ct = default
    );
}
