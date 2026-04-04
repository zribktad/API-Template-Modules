using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Features.Product.CreateProducts;

/// <summary>
///     Carries the data required to create a new product, including validation constraints enforced via data annotations.
/// </summary>
public sealed record CreateProductRequest(
    [NotEmpty(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
        string Name,
    string? Description,
    [Range(0.0, double.MaxValue, ErrorMessage = "Price must be non-negative.")] decimal Price,
    Guid? CategoryId = null,
    IReadOnlyCollection<Guid>? ProductDataIds = null
) : IProductRequest;
