using BuildingBlocks.Application.Validation;
using BuildingBlocks.Domain.Entities.Contracts;

namespace BuildingBlocks.Application.Batch.Rules;

public sealed class DataAnnotationsBatchRule<TItem>(IValidator validator) : IBatchRule<TItem>
{
    public Task ApplyAsync(BatchFailureContext<TItem> context, CancellationToken ct)
    {
        for (int i = 0; i < context.Items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (context.IsFailed(i))
                continue;

            IReadOnlyList<System.ComponentModel.DataAnnotations.ValidationResult> validationResults =
                validator.Validate(context.Items[i]!);
            if (validationResults.Count == 0)
                continue;

            Guid? id = context.Items[i] is IHasId hasId ? hasId.Id : null;
            context.AddFailure(
                i,
                id,
                validationResults
                    .Select(result => result.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Cast<string>()
                    .ToList()
            );
        }

        return Task.CompletedTask;
    }
}

