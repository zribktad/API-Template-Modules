using Identity.Application.Features.User.DTOs;
using SharedKernel.Domain.Entities.Contracts;
using Identity.Application.Features.User.Specifications;
using Identity.Domain.Interfaces;
using ErrorOr;

namespace Identity.Application.Features.User;

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
