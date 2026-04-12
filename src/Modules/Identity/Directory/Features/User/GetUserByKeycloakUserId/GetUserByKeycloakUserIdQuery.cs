using ErrorOr;
using Identity.Errors;

namespace Identity.Directory.Features.User;

public sealed record GetUserByKeycloakUserIdQuery(string KeycloakUserId);

public sealed class GetUserByKeycloakUserIdQueryHandler
{
    public static async Task<ErrorOr<UserResponse>> HandleAsync(
        GetUserByKeycloakUserIdQuery request,
        IUserRepository repository,
        CancellationToken ct
    )
    {
        UserResponse? result = await repository.FirstOrDefaultAsync(
            new UserByKeycloakUserIdAsUserResponseSpecification(request.KeycloakUserId),
            ct
        );
        if (result is null)
            return DomainErrors.Users.NotFoundByKeycloakUserId(request.KeycloakUserId);

        return result;
    }
}
