using ErrorOr;
using Identity.Application.Common.Security;
using Identity.Domain;
using Identity.Domain.Interfaces;
using SharedKernel.Application.Events;
using SharedKernel.Application.Extensions;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Domain.Interfaces;
using Wolverine;

namespace Identity.Application.Features.User;

public sealed record SetUserActiveCommand(Guid Id, bool IsActive) : IHasId;

public sealed class SetUserActiveCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        SetUserActiveCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IKeycloakAdminService keycloakAdmin,
        CancellationToken ct
    )
    {
        var userResult = await repository.GetByIdOrError(
            command.Id,
            DomainErrors.Users.NotFound(command.Id),
            ct
        );
        if (userResult.IsError)
            return (userResult.Errors, OutgoingMessagesHelper.Empty);
        var user = userResult.Value;

        if (user.KeycloakUserId is not null)
            await keycloakAdmin.SetUserEnabledAsync(user.KeycloakUserId, command.IsActive, ct);

        user.IsActive = command.IsActive;
        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
