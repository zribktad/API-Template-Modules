using Identity.Features.User.DTOs;
using SharedKernel.Domain.Entities.Contracts;
using Identity.Features.User.Specifications;
using Identity.Interfaces;
using ErrorOr;

namespace Identity.Features.User;

public sealed record GetUserByIdQuery(Guid Id) : IHasId;

public sealed class GetUserByIdQueryHandler
{
    public static async Task<ErrorOr<UserResponse>> HandleAsync(
        GetUserByIdQuery request,
        IUserRepository repository,
        CancellationToken ct
    )
    {
        UserResponse? result = await repository.FirstOrDefaultAsync(
            new UserByIdSpecification(request.Id),
            ct
        );
        if (result is null)
            return DomainErrors.Users.NotFound(request.Id);

        return result;
    }
}

