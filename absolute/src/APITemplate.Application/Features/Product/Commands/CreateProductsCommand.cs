using APITemplate.Application.Common.Batch;
using APITemplate.Application.Common.Batch.Rules;
using APITemplate.Application.Common.Events;
using APITemplate.Domain.Entities;
using ErrorOr;
using FluentValidation;
using Wolverine;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

/// <summary>Creates multiple products in a single batch operation.</summary>
public sealed record CreateProductsCommand(CreateProductsRequest Request);

/// <summary>Handles <see cref="CreateProductsCommand"/> by validating all items, bulk-validating references, and persisting in a single transaction.</summary>
public sealed class CreateProductsCommandHandler
{
    public static async Task<ErrorOr<BatchResponse>> HandleAsync(
        CreateProductsCommand command,
        IProductRepository repository,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IValidator<CreateProductRequest> itemValidator,
        CancellationToken ct
    )
    {
        var items = command.Request.Items;
        var context = new BatchFailureContext<CreateProductRequest>(items);

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
            return context.ToFailureResponse();

        // Build entities and persist in a single transaction
        var entities = items
            .Select(item =>
            {
                var productId = Guid.NewGuid();
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

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.AddRangeAsync(entities, ct);
            },
            ct
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Products));
        return new BatchResponse([], items.Count, 0);
    }
}
