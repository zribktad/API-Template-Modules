namespace SharedKernel.Application.Batch;

public interface IBatchRule<TItem>
{
    public Task ApplyAsync(BatchFailureContext<TItem> context, CancellationToken ct);
}
