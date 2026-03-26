using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Errors;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Extensions;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace APITemplate.Application.Features.Tenant;

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
