using Identity.Domain.Entities;
using SharedKernel.Application.Sorting;

namespace Identity.Application.Features.User;

/// <summary>
/// Defines the sortable fields available for user queries and maps them to entity property expressions.
/// </summary>
public static class UserSortFields
{
    public static readonly SortField Username = new("username");
    public static readonly SortField Email = new("email");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<AppUser> Map = new SortFieldMap<AppUser>()
        .Add(Username, u => u.Username)
        .Add(Email, u => u.Email)
        .Add(CreatedAt, u => u.Audit.CreatedAtUtc)
        .Default(u => u.Audit.CreatedAtUtc);
}
