using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Product.UpdateProducts;

/// <summary>
///     Carries the replacement data for an existing product, subject to the same validation constraints as
///     <see cref="CreateProductRequest" />.
/// </summary>
public sealed record UpdateProductRequest(
    [NotEmpty(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
        string Name,
    [RequiredWhenDecimalPropertyExceeds(
        nameof(Price),
        1000,
        ErrorMessage = "Description is required for products priced above 1000."
    )]
    string? Description,
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Price must be non-negative.")] decimal Price,
    Guid? CategoryId = null,
    IReadOnlyCollection<Guid>? ProductDataIds = null
) : IProductRequest;
