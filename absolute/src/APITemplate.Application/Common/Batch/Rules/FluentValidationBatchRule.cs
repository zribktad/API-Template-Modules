using FluentValidation;

namespace APITemplate.Application.Common.Batch.Rules;

internal sealed class FluentValidationBatchRule<TItem>(IValidator<TItem> validator)
    : IBatchRule<TItem>
{
    private readonly IValidator<TItem> _validator = validator;

    public async Task ApplyAsync(BatchFailureContext<TItem> context, CancellationToken ct)
    {
        for (var i = 0; i < context.Items.Count; i++)
        {
            if (context.IsFailed(i))
                continue;

            var validationResult = await _validator.ValidateAsync(context.Items[i], ct);
            if (!validationResult.IsValid)
            {
                Guid? id = context.Items[i] is IHasId hasId ? hasId.Id : null;
                context.AddFailure(
                    i,
                    id,
                    validationResult.Errors.Select(error => error.ErrorMessage).ToList()
                );
            }
        }
    }
}
