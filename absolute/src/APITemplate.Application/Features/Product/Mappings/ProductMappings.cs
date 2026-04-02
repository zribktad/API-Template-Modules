using System.Linq.Expressions;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Mappings;

/// <summary>
/// Provides EF Core-compatible projection expressions and in-memory mapping helpers for converting <c>Product</c> domain entities to <see cref="ProductResponse"/> DTOs.
/// </summary>
public static class ProductMappings
{
    /// <summary>
    /// LINQ expression that projects a <c>Product</c> entity to a <see cref="ProductResponse"/>; safe to pass directly into EF Core queries.
    /// </summary>
    public static readonly Expression<Func<ProductEntity, ProductResponse>> Projection =
        p => new ProductResponse(
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.CategoryId,
            p.Audit.CreatedAtUtc,
            p.ProductDataLinks.Select(link => link.ProductDataId).ToArray()
        );

    private static readonly Func<ProductEntity, ProductResponse> CompiledProjection =
        Projection.Compile();

    /// <summary>Maps a fully-loaded <c>Product</c> entity to a <see cref="ProductResponse"/> using the pre-compiled projection.</summary>
    public static ProductResponse ToResponse(this ProductEntity product) =>
        CompiledProjection(product);
}
