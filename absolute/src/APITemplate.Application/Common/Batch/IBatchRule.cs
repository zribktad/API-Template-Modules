namespace APITemplate.Application.Common.Batch;

internal interface IBatchRule<TItem>
{
    Task ApplyAsync(BatchFailureContext<TItem> context, CancellationToken ct);
}
