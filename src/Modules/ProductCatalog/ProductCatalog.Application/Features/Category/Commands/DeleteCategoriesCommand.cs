using ErrorOr;
using ProductCatalog.Application.Features.Category.Specifications;
using ProductCatalog.Domain;
using Wolverine;

namespace ProductCatalog.Application.Features.Category;

/// <summary>Soft-deletes multiple categories in a single batch operation.</summary>
public sealed record DeleteCategoriesCommand(BatchDeleteRequest Request);

/// <summary>Handles <see cref="DeleteCategoriesCommand"/> by loading all categories and deleting in a single transaction.</summary>
public sealed class DeleteCategoriesCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<ProductCatalog.Domain.Entities.Category>?,
        OutgoingMessages
    )> LoadAsync(
        DeleteCategoriesCommand command,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> ids = command.Request.Ids;
        BatchFailureContext<Guid> context = new(ids);

        // Load all target categories and mark missing ones as failed
        IReadOnlyList<ProductCatalog.Domain.Entities.Category> categories =
            await repository.ListAsync(new CategoriesByIdsSpecification(ids.ToHashSet()), ct);

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<Guid>(
                id => id,
                categories.Select(category => category.Id).ToHashSet(),
                ErrorCatalog.Categories.NotFoundMessage
            )
        );

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (HandlerContinuation.Continue, categories, OutgoingMessagesHelper.Empty);
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        DeleteCategoriesCommand command,
        IReadOnlyList<ProductCatalog.Domain.Entities.Category> categories,
        ICategoryRepository repository,
        IProductRepository productRepository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteRangeAsync(categories, ct);
                await productRepository.ClearCategoryAsync(
                    categories.Select(category => category.Id).ToArray(),
                    ct
                );
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));

        return (new BatchResponse([], command.Request.Ids.Count, 0), messages);
    }
}
