using APITemplate.Application.Common.Batch;
using APITemplate.Application.Common.Batch.Rules;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Category.Specifications;
using ErrorOr;
using FluentValidation;
using Wolverine;

namespace APITemplate.Application.Features.Category;

/// <summary>Updates multiple categories in a single batch operation.</summary>
public sealed record UpdateCategoriesCommand(UpdateCategoriesRequest Request);

/// <summary>Handles <see cref="UpdateCategoriesCommand"/> by validating all items, loading categories in bulk, and updating in a single transaction.</summary>
public sealed class UpdateCategoriesCommandHandler
{
    public static async Task<ErrorOr<BatchResponse>> HandleAsync(
        UpdateCategoriesCommand command,
        ICategoryRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IValidator<UpdateCategoryItem> itemValidator,
        CancellationToken ct
    )
    {
        var items = command.Request.Items;
        var context = new BatchFailureContext<UpdateCategoryItem>(items);
        await context.ApplyRulesAsync(
            ct,
            new FluentValidationBatchRule<UpdateCategoryItem>(itemValidator)
        );

        // Load all target categories and mark missing ones as failed
        var requestedIds = items
            .Where((_, i) => !context.IsFailed(i))
            .Select(item => item.Id)
            .ToHashSet();
        var categoryMap = (
            await repository.ListAsync(new CategoriesByIdsSpecification(requestedIds), ct)
        ).ToDictionary(c => c.Id);

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<UpdateCategoryItem>(
                item => item.Id,
                categoryMap.Keys.ToHashSet(),
                ErrorCatalog.Categories.NotFoundMessage
            )
        );

        if (context.HasFailures)
            return context.ToFailureResponse();

        // Apply changes in a single transaction
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var category = categoryMap[item.Id];

                    category.Name = item.Name;
                    category.Description = item.Description;

                    await repository.UpdateAsync(category, ct);
                }
            },
            ct
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Categories));

        return new BatchResponse([], items.Count, 0);
    }
}
