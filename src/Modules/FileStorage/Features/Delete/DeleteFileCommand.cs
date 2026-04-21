using FileStorage.Domain.Sagas;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace FileStorage.Features.Delete;

/// <summary>Soft-deletes a <see cref="StoredFile" /> and cascades a refcount check for the backing blob.</summary>
public sealed record DeleteFileCommand(Guid Id);

/// <summary>
///     Handler that participates in Wolverine's EF Core transactional middleware:
///     <see cref="FileStorageDbContext" /> is injected directly so <c>SaveChangesAsync</c>, the outbox
///     write for <see cref="MaybeDeleteBlobCommand" />, and the soft-delete UPDATE all commit in the same
///     DB transaction. Prevents a crash between tx commit and cascade dispatch from leaking blobs.
/// </summary>
public sealed class DeleteFileCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteFileCommand command,
        FileStorageDbContext dbContext,
        CancellationToken ct
    )
    {
        StoredFile? entity = await dbContext.StoredFiles.FirstOrDefaultAsync(
            f => f.Id == command.Id,
            ct
        );
        if (entity is null)
            return (DomainErrors.Files.FileNotFound(command.Id.ToString()), new OutgoingMessages());

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;

        OutgoingMessages messages = new();
        messages.Add(new MaybeDeleteBlobCommand(entity.TenantId, entity.Sha256, entity.BackendKey));

        return (Result.Success, messages);
    }
}
