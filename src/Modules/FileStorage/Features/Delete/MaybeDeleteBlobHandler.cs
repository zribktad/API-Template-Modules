using FileStorage.Domain.Sagas;
using FileStorage.Domain.Storage;
using Microsoft.Extensions.Logging;

namespace FileStorage.Features.Delete;

/// <summary>
///     Refcount-aware blob delete. Invoked as a cascading message from the soft-delete flow; counts active
///     <see cref="StoredFile" /> rows referencing the hash within the tenant. If none remain, removes the
///     physical blob via <see cref="IBlobStore.DeleteAsync" /> — idempotent if the blob is already gone.
/// </summary>
public sealed class MaybeDeleteBlobHandler
{
    public static async Task HandleAsync(
        MaybeDeleteBlobCommand command,
        IStoredFileRepository repository,
        IBlobStoreFactory blobStoreFactory,
        ILogger<MaybeDeleteBlobHandler> logger,
        CancellationToken ct
    )
    {
        int refcount = await repository.CountAsync(
            new ActiveStoredFilesBySha256AndTenantSpecification(command.TenantId, command.Sha256),
            ct
        );

        if (refcount > 0)
        {
            logger.LogDebug(
                "Blob {Sha256} for tenant {TenantId} still has {Refcount} active references; skip delete",
                command.Sha256,
                command.TenantId,
                refcount
            );
            return;
        }

        IBlobStore store = blobStoreFactory.Get(command.BackendKey);
        await store.DeleteAsync(command.TenantId, command.Sha256, ct);

        logger.LogInformation(
            "Deleted orphan blob {Sha256} for tenant {TenantId}",
            command.Sha256,
            command.TenantId
        );
    }
}
