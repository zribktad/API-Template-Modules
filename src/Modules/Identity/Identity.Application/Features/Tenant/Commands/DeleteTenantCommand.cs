using Identity.Domain.Interfaces;
using SharedKernel.Application.Context;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Events;
using SharedKernel.Application.Extensions;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Application.Features.Tenant;

public sealed record DeleteTenantCommand(Guid Id) : IHasId;

public sealed class DeleteTenantCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        DeleteTenantCommand command,
        ITenantRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        ILogger<DeleteTenantCommandHandler> logger,
        CancellationToken ct
    )
    {
        var tenantResult = await repository.GetByIdOrError(
            command.Id,
            DomainErrors.Tenants.NotFound(command.Id),
            ct
        );
        if (tenantResult.IsError)
            return tenantResult.Errors;

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteAsync(tenantResult.Value, ct);
            },
            ct
        );

        await bus.PublishSafeAsync(
            new TenantSoftDeletedNotification(
                command.Id,
                actorProvider.ActorId,
                timeProvider.GetUtcNow().UtcDateTime
            ),
            logger
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Tenants));
        return Result.Success;
    }
}
