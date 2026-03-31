using APITemplate.Application.Features.User.Mappings;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

/// <summary>
/// Ardalis specification that retrieves a filtered and sorted list of users projected to <see cref="UserResponse"/>.
/// </summary>
public sealed class UserFilterSpecification : Specification<AppUser, UserResponse>
{
    /// <summary>
    /// Initialises the specification by applying filter criteria, sort order, and projection from the given <paramref name="filter"/>.
    /// </summary>
    public UserFilterSpecification(UserFilter filter)
    {
        Query.ApplyFilter(filter);
        Query.AsNoTracking();

        UserSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        Query.Select(UserMappings.Projection);
    }
}
