using APITemplate.Application.Common.Errors;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Application.Features.Product.Specifications;
using ErrorOr;

namespace APITemplate.Application.Features.Product;

/// <summary>Retrieves a single product by its unique identifier.</summary>
public sealed record GetProductByIdQuery(Guid Id) : IHasId;

/// <summary>Handles <see cref="GetProductByIdQuery"/> by fetching from the product repository.</summary>
public sealed class GetProductByIdQueryHandler
{
    public static async Task<ErrorOr<ProductResponse>> HandleAsync(
        GetProductByIdQuery request,
        IProductRepository repository,
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
