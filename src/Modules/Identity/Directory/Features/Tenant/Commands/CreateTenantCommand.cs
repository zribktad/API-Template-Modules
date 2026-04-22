using ErrorOr;
using Identity.Directory.Domain.Services;
using Identity.Directory.Features.Tenant.Mappings;
using Identity.Directory.Repositories;
using Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using TenantEntity = Identity.Directory.Entities.Tenant;

namespace Identity.Directory.Features.Tenant;

public sealed record CreateTenantCommand(CreateTenantRequest Request);

public sealed class CreateTenantCommandHandler
{
    public static async Task<(ErrorOr<TenantResponse>, OutgoingMessages)> HandleAsync(
        CreateTenantCommand command,
        ITenantRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ITenantUniquenessChecker uniquenessChecker,
        CancellationToken ct
    )
    {
        ErrorOr<TenantCode> codeResult = TenantCode.Create(command.Request.Code);
        if (codeResult.IsError)
            return (codeResult.FirstError, OutgoingMessagesHelper.Empty);

        ErrorOr<Success> uniquenessResult = await uniquenessChecker.EnsureCodeUniqueAsync(
            codeResult.Value,
            ct
        );
        if (uniquenessResult.IsError)
            return (uniquenessResult.FirstError, OutgoingMessagesHelper.Empty);

        TenantEntity tenant;
        try
        {
            tenant = await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    TenantEntity entity = TenantEntity.Create(
                        Guid.NewGuid(),
                        codeResult.Value,
                        command.Request.Name
                    );

                    await repository.AddAsync(entity, ct);
                    return entity;
                },
                ct
            );
        }
        catch (DbUpdateException ex) when (ex.IsTenantCodeUniqueViolation())
        {
            return (DomainErrors.Tenants.CodeAlreadyExists(codeResult.Value.Value), OutgoingMessagesHelper.Empty);
        }

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Tenants));
        return (tenant.ToResponse(), messages);
    }
}
