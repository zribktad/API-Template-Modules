using APITemplate.Application.Common.Errors;
using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities.Contracts;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.User;

public sealed record GetUserByIdQuery(Guid Id) : IHasId;

public sealed class GetUserByIdQueryHandler
{
    public static async Task<ErrorOr<UserResponse>> HandleAsync(
        GetUserByIdQuery request,
        IUserRepository repository,
        CancellationToken ct
    )
    {
        var result = await repository.FirstOrDefaultAsync(
            new UserByIdSpecification(request.Id),
            ct
        );
        if (result is null)
            return DomainErrors.Users.NotFound(request.Id);

        return result;
    }
}
