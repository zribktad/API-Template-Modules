using Contracts.Queries.ProductCatalog;
using ErrorOr;
using ProductCatalog.Application.Features.Product.Repositories;
using ProductEntity = ProductCatalog.Domain.Entities.Product;

namespace ProductCatalog.Application.Features.Product.Queries;

public sealed class ValidateProductExistsQueryHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        ValidateProductExistsQuery query,
        Repositories.IProductRepository repository,
        CancellationToken ct
    )
    {
        ProductEntity? product = await repository.GetByIdAsync(query.ProductId, ct);

        if (product is null)
            return DomainErrors.Products.NotFound(query.ProductId);

        return Result.Success;
    }
}
