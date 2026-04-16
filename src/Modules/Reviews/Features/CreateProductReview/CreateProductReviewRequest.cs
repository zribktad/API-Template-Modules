using System.ComponentModel.DataAnnotations;

namespace Reviews.Features;

/// <summary>
///     Payload for submitting a new product review, including the target product, an optional comment, and a 1–5 star
///     rating.
/// </summary>
/// <remarks>
///     Request-body DTO for a Wolverine HTTP endpoint — declared as a class-style record with <c>{ get; init; }</c>
///     properties. The JSON body binder honors <c>init</c> setters, and validation attributes sit directly on the
///     generated properties, so <c>Validator.TryValidateObject</c> picks them up with no attribute-target gymnastics.
///     See <c>docs/validation.md</c> — Record DTO convention.
/// </remarks>
public sealed record CreateProductReviewRequest
{
    [NotEmpty(ErrorMessage = "ProductId is required.")]
    public Guid ProductId { get; init; }

    public string? Comment { get; init; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; init; }
}
