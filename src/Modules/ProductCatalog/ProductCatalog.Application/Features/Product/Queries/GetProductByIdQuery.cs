using SharedKernel.Application.Errors;
using ProductCatalog.Application.Features.Product.Repositories;
using ProductCatalog.Application.Features.Product.Specifications;
using ErrorOr;
using ProductRepositoryContract = ProductCatalog.Application.Features.Product.Repositories.IProductRepository;

namespace ProductCatalog.Application.Features.Product;

/// <summary>Retrieves a single product by its unique identifier.</summary>
public sealed record GetProductByIdQuery(Guid Id) : IHasId;

/// <summary>Handles <see cref="GetProductByIdQuery"/> by fetching from the product repository.</summary>
public sealed class GetProductByIdQueryHandler
{
    public static async Task<ErrorOr<ProductResponse>> HandleAsync(
        GetProductByIdQuery request,
        ProductRepositoryContract repository,
        CancellationToken ct
    )
    {
        var result = await repository.FirstOrDefaultAsync(
            new ProductByIdSpecification(request.Id),
            ct
        );

        if (result is null)
            return DomainErrors.Products.NotFound(request.Id);

        return result;
    }
}


