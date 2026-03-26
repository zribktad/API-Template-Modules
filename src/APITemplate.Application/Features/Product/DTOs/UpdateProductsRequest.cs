using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Carries a list of product items to be updated in a single batch operation; accepts between 1 and 100 items.
/// </summary>
public sealed record UpdateProductsRequest(
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(100, ErrorMessage = "Maximum 100 items per batch.")]
        IReadOnlyList<UpdateProductItem> Items
);

/// <summary>
/// Represents a single product within a batch update request, including its ID and replacement data.
/// </summary>
public sealed record UpdateProductItem(
    [NotEmpty(ErrorMessage = "Product ID is required.")] Guid Id,
    [NotEmpty(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
        string Name,
    string? Description,
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")] decimal Price,
    Guid? CategoryId = null,
    IReadOnlyCollection<Guid>? ProductDataIds = null
) : IProductRequest, IHasId;
