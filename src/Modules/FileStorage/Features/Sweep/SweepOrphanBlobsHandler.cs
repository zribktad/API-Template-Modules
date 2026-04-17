using SharedKernel.Contracts.Commands.FileStorage;

namespace FileStorage.Features.Sweep;

/// <summary>
///     Wolverine handler for <see cref="SweepOrphanBlobsCommand" />. Delegates to
///     <see cref="IOrphanBlobSweepService" /> which performs the filesystem + DB refcount sweep.
/// </summary>
public sealed class SweepOrphanBlobsHandler
{
    public static Task HandleAsync(
        SweepOrphanBlobsCommand _,
        IOrphanBlobSweepService sweeper,
        CancellationToken ct
    )
    {
        return sweeper.SweepAsync(ct);
    }
}
