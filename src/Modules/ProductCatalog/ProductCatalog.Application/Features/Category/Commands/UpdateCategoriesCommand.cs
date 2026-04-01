using Contracts.Events;
using ErrorOr;
using FluentValidation;
using ProductCatalog.Application.Features.Category.Specifications;
using ProductCatalog.Domain;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using Wolverine;

namespace ProductCatalog.Application.Features.Category;

/// <summary>Updates multiple categories in a single batch operation.</summary>
public sealed record UpdateCategoriesCommand(UpdateCategoriesRequest Request);

/// <summary>Handles <see cref="UpdateCategoriesCommand"/> by validating all items, loading categories in bulk, and updating in a single transaction.</summary>
public sealed class UpdateCategoriesCommandHandler
{
    public sealed record UpdateCategoriesState(
        IReadOnlyList<UpdateCategoryItem> Items,
        IReadOnlyDictionary<Guid, ProductCatalog.Domain.Entities.Category> CategoryMap
    );

    public static async Task<(
        HandlerContinuation,
        UpdateCategoriesState?,
        OutgoingMessages
    )> LoadAsync(
        UpdateCategoriesCommand command,
        ICategoryRepository repository,
        IValidator<UpdateCategoryItem> itemValidator,
        CancellationToken ct
    )
    {
        IReadOnlyList<UpdateCategoryItem> items = command.Request.Items;
        BatchFailureContext<UpdateCategoryItem> context = new(items);
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
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (
            HandlerContinuation.Continue,
            new UpdateCategoriesState(items, categoryMap),
            new OutgoingMessages()
        );
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        UpdateCategoriesCommand command,
        UpdateCategoriesState state,
        ICategoryRepository repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        // Apply changes in a single transaction
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                for (int i = 0; i < state.Items.Count; i++)
                {
                    UpdateCategoryItem item = state.Items[i];
                    ProductCatalog.Domain.Entities.Category category = state.CategoryMap[item.Id];

                    category.Name = item.Name;
                    category.Description = item.Description;

                    await repository.UpdateAsync(category, ct);
                }
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));

        return (new BatchResponse([], command.Request.Items.Count, 0), messages);
    }
}
