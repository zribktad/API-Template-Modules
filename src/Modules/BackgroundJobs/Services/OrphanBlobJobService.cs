using BackgroundJobs.Contracts;
using SharedKernel.Contracts.Commands.FileStorage;
using Wolverine;

namespace BackgroundJobs.Services;

/// <summary>
///     Default <see cref="IOrphanBlobJobService" /> — simply publishes a
///     <see cref="SweepOrphanBlobsCommand" /> which the FileStorage module handles.
/// </summary>
public sealed class OrphanBlobJobService : IOrphanBlobJobService
{
    private readonly IMessageBus _bus;

    public OrphanBlobJobService(IMessageBus bus)
    {
        _bus = bus;
    }

    public Task RunSweepAsync(CancellationToken ct) =>
        _bus.InvokeAsync(new SweepOrphanBlobsCommand(), ct);
}
