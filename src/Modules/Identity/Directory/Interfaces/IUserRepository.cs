namespace Identity.Directory.Interfaces;

/// <summary>
///     Repository contract for <see cref="AppUser" /> entities with user-specific lookup operations.
/// </summary>
public interface IUserRepository : IRepository<AppUser>
{
    /// <summary>Returns <c>true</c> if a user with the given email (case-insensitive) already exists.</summary>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> if a user with the given normalised username already exists.</summary>
    public Task<bool> ExistsByUsernameAsync(
        string normalizedUsername,
        CancellationToken ct = default
    );

    /// <summary>Returns the user whose normalised email matches the given address, or <c>null</c> if not found.</summary>
    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    ///     Distinct permission names for the user matching the JWT <c>sub</c> (Keycloak id or application user id).
    ///     Ignores tenant filters — used only from <c>IClaimsTransformation</c> where there is no tenant scope.
    /// </summary>
    public Task<IReadOnlyList<string>> ListDistinctPermissionNamesBySubjectAsync(
        string subject,
        CancellationToken ct = default
    );
}
