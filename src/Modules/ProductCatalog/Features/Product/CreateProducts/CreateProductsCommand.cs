using ErrorOr;
using ProductCatalog.Domain.Services;
using Wolverine;
using ProductEntity = ProductCatalog.Entities.Product;
using ProductRepositoryContract = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog.Features.Product.CreateProducts;

/// <summary>Creates multiple products in a single batch operation.</summary>
public sealed record CreateProductsCommand(CreateProductsRequest Request);

/// <summary>
///     Handles <see cref="CreateProductsCommand" /> by delegating validation and entity construction to
///     <see cref="IProductBatchFactory" /> and persisting in a single transaction.
/// </summary>
public sealed class CreateProductsCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<ProductEntity>?,
        OutgoingMessages
    )> LoadAsync(CreateProductsCommand command, IProductBatchFactory factory, CancellationToken ct)
    {
        ErrorOr<IReadOnlyList<ProductEntity>> result = await factory.CreateAsync(
            command.Request.Items,
            ct
        );

        if (result.IsError)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(BatchResponseError.Unwrap(result.FirstError));
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (HandlerContinuation.Continue, result.Value, OutgoingMessagesHelper.Empty);
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        CreateProductsCommand command,
        IReadOnlyList<ProductEntity> entities,
        ProductRepositoryContract repository,
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
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (new BatchResponse([], command.Request.Items.Count, 0), messages);
    }
}
