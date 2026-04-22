namespace Identity.Directory.Features.User;

/// <summary>
///     Defines the sortable fields available for user queries and maps them to entity property expressions.
/// </summary>
public static class UserSortFields
{
    public const string UsernameToken = nameof(Username);
    public const string EmailToken = nameof(Email);
    public const string CreatedAtToken = nameof(CreatedAt);

    public static readonly SortField Username = new(UsernameToken);
    public static readonly SortField Email = new(EmailToken);
    public static readonly SortField CreatedAt = new(CreatedAtToken);

    public static readonly SortFieldMap<AppUser> Map = new SortFieldMap<AppUser>()
        .Add(Username, u => u.Username.Value)
        .Add(Email, u => u.Email.Value)
        .Add(CreatedAt, u => u.Audit.CreatedAtUtc)
        .Default(u => u.Audit.CreatedAtUtc);
}
