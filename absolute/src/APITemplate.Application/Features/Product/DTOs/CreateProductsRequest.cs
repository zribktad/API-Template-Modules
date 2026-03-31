using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Carries a list of product items to be created in a single batch operation; accepts between 1 and 100 items.
/// </summary>
public sealed record CreateProductsRequest(
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(100, ErrorMessage = "Maximum 100 items per batch.")]
        IReadOnlyList<CreateProductRequest> Items
);
