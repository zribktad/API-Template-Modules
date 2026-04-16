using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.DTOs;

/// <summary>
///     Reusable pagination input carried by list query requests.
///     Data-annotation constraints enforce valid ranges at the HTTP boundary and in explicit object validation flows.
/// </summary>
/// <remarks>
///     Validation attributes use the <c>[property: ...]</c> target so they land on the generated properties and are
///     visible to <c>Validator.TryValidateObject</c> (which Wolverine HTTP uses). MVC reads attributes from both ctor
///     parameters and properties, so this change is transparent for MVC-bound derived filters.
///     See <c>docs/validation.md</c> — Record DTO convention.
/// </remarks>
public record PaginationFilter(
    [property: Range(
        1,
        int.MaxValue,
        ErrorMessage = "PageNumber must be greater than or equal to 1."
    )]
        int PageNumber = 1,
    [property: Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
        int PageSize = 20
)
{
    /// <summary>Default page size applied when none is specified by the caller.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum allowed page size to prevent unbounded queries.</summary>
    public const int MaxPageSize = 100;
}
