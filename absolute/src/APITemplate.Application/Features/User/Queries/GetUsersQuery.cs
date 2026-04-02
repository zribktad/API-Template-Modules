using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.User;

public sealed record GetUsersQuery(UserFilter Filter);

public sealed class GetUsersQueryHandler
{
    public static async Task<ErrorOr<PagedResponse<UserResponse>>> HandleAsync(
        GetUsersQuery request,
        IUserRepository repository,
        CancellationToken ct
    )
    {
        return await repository.GetPagedAsync(
            new UserFilterSpecification(request.Filter),
            request.Filter.PageNumber,
            request.Filter.PageSize,
            ct
        );
    }
}
