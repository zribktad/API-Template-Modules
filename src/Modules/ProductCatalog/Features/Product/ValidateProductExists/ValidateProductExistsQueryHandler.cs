using ErrorOr;
using SharedKernel.Contracts.Queries.ProductCatalog;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Product.ValidateProductExists;

public sealed class ValidateProductExistsQueryHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        ValidateProductExistsQuery query,
        IProductRepository repository,
        CancellationToken ct
    )
    {
        ProductEntity? product = await repository.GetByIdAsync(query.ProductId, ct);

        if (product is null)
            return DomainErrors.Products.NotFound(query.ProductId);

        return Result.Success;
    }
}
