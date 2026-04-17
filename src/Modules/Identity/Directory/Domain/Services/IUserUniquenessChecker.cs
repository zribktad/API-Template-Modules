using ErrorOr;
using Identity.ValueObjects;

namespace Identity.Directory.Domain.Services;

/// <summary>
///     Enforces uniqueness invariants for <see cref="AppUser" /> email and username.
///     Repositories expose raw existence queries (<c>ExistsBy*Async</c>); this service adds the business rule layer
///     — mapping conflicts to <see cref="DomainErrors.Users" /> error codes and choosing the check order.
/// </summary>
public interface IUserUniquenessChecker
{
    /// <summary>
    ///     Returns an error if the given email is already taken by another user.
    /// </summary>
    Task<ErrorOr<Success>> EnsureEmailUniqueAsync(Email email, CancellationToken ct = default);

    /// <summary>
    ///     Returns an error if the given username (after normalization) is already taken by another user.
    /// </summary>
    Task<ErrorOr<Success>> EnsureUsernameUniqueAsync(
        string username,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Checks email first, then username. Short-circuits on the first conflict so the caller sees a single error.
    /// </summary>
    Task<ErrorOr<Success>> EnsureUniqueAsync(
        string username,
        Email email,
        CancellationToken ct = default
    );
}
