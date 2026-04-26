using ErrorOr;
using SharedKernel.Contracts.Queries.ProductCatalog;

namespace ProductCatalog.Features.Product.ValidateProductExists;

public sealed class ValidateProductExistsQueryHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        ValidateProductExistsQuery query,
        IProductRepository repository,
        CancellationToken ct
    )
    {
        if (!await repository.ExistsByIdAsync(query.ProductId, ct))
            return DomainErrors.Products.NotFound(query.ProductId);

        return Result.Success;
    }
}
