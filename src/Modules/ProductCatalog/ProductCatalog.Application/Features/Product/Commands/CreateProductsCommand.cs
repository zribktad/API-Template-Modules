using Contracts.Events;
using ErrorOr;
using FluentValidation;
using ProductCatalog.Domain;
using ProductCatalog.Domain.Entities;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using Wolverine;
using ProductEntity = ProductCatalog.Domain.Entities.Product;
using ProductRepositoryContract = ProductCatalog.Application.Features.Product.Repositories.IProductRepository;

namespace ProductCatalog.Application.Features.Product;

/// <summary>Creates multiple products in a single batch operation.</summary>
public sealed record CreateProductsCommand(CreateProductsRequest Request);

/// <summary>Handles <see cref="CreateProductsCommand"/> by validating all items, bulk-validating references, and persisting in a single transaction.</summary>
public sealed class CreateProductsCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<ProductEntity>?,
        OutgoingMessages
    )> LoadAsync(
        CreateProductsCommand command,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IValidator<CreateProductRequest> itemValidator,
        CancellationToken ct
    )
    {
        IReadOnlyList<CreateProductRequest> items = command.Request.Items;
        BatchFailureContext<CreateProductRequest> context = new(items);

        await context.ApplyRulesAsync(
            ct,
            new FluentValidationBatchRule<CreateProductRequest>(itemValidator)
        );

        // Reference checks skip only fluent-validation failures so both category and
        // product-data issues can be reported for the same index (merged into one failure row).
        context.AddFailures(
            await ProductValidationHelper.CheckProductReferencesAsync(
                items,
                categoryRepository,
                productDataRepository,
                context.FailedIndices,
                ct
            )
        );

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        List<ProductEntity> entities = items
            .Select(item =>
            {
                Guid productId = Guid.NewGuid();
                return new ProductEntity
                {
                    Id = productId,
                    Name = item.Name,
                    Description = item.Description,
                    Price = item.Price,
                    CategoryId = item.CategoryId,
                    ProductDataLinks = (item.ProductDataIds ?? [])
                        .Distinct()
                        .Select(pdId => ProductDataLink.Create(productId, pdId))
                        .ToList(),
                };
            })
            .ToList();

        return (HandlerContinuation.Continue, entities, new OutgoingMessages());
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
