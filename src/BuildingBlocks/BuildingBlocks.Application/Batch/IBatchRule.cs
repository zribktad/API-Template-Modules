namespace BuildingBlocks.Application.Batch;

public interface IBatchRule<TItem>
{
    Task ApplyAsync(BatchFailureContext<TItem> context, CancellationToken ct);
}
