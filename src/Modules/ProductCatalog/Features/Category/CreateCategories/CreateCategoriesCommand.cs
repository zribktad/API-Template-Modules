using ErrorOr;
using Wolverine;
using CategoryEntity = ProductCatalog.Entities.Category;

namespace ProductCatalog.Features.Category.CreateCategories;

/// <summary>Creates multiple categories in a single batch operation.</summary>
public sealed record CreateCategoriesCommand(CreateCategoriesRequest Request);

/// <summary>
///     Handles <see cref="CreateCategoriesCommand" /> by persisting validated request items in a single transaction.
/// </summary>
public sealed class CreateCategoriesCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<CategoryEntity>?,
        OutgoingMessages
    )> LoadAsync(
        CreateCategoriesCommand command,
        CancellationToken ct
    )
    {
        IReadOnlyList<CreateCategoryRequest> items = command.Request.Items;
        List<CategoryEntity> entities = items
            .Select(item => CategoryEntity.Create(item.Name, item.Description))
            .ToList();

        return (HandlerContinuation.Continue, entities, OutgoingMessagesHelper.Empty);
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        CreateCategoriesCommand command,
        IReadOnlyList<CategoryEntity> entities,
        ICategoryRepository repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.AddRangeAsync(entities, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (new BatchResponse([], command.Request.Items.Count, 0), messages);
    }
}
