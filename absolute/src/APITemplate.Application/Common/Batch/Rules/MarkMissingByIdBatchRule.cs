namespace APITemplate.Application.Common.Batch.Rules;

internal sealed class MarkMissingByIdBatchRule<TItem>(
    Func<TItem, Guid> idSelector,
    IReadOnlySet<Guid> foundIds,
    string notFoundMessageTemplate
) : IBatchRule<TItem>
{
    public Task ApplyAsync(BatchFailureContext<TItem> context, CancellationToken ct)
    {
        for (var i = 0; i < context.Items.Count; i++)
        {
            if (context.IsFailed(i))
                continue;

            Guid id = idSelector(context.Items[i]);
            if (!foundIds.Contains(id))
                context.AddFailure(i, id, string.Format(notFoundMessageTemplate, id));
        }

        return Task.CompletedTask;
    }
}
