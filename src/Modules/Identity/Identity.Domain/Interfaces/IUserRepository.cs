using Identity.Domain.Entities;
using SharedKernel.Domain.Interfaces;

namespace Identity.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="AppUser"/> entities with user-specific lookup operations.
/// </summary>
public interface IUserRepository : IRepository<AppUser>
{
    /// <summary>Returns <c>true</c> if a user with the given email (case-insensitive) already exists.</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> if a user with the given normalised username already exists.</summary>
    Task<bool> ExistsByUsernameAsync(string normalizedUsername, CancellationToken ct = default);

    /// <summary>Returns the user whose normalised email matches the given address, or <c>null</c> if not found.</summary>
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default);
}
