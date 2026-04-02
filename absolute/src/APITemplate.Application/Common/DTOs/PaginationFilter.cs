using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.DTOs;

/// <summary>
/// Reusable pagination input carried by list query requests.
/// Data-annotation constraints enforce valid ranges so FluentValidation and model binding both reject bad input.
/// </summary>
public record PaginationFilter(
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than or equal to 1.")]
        int PageNumber = 1,
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")] int PageSize = 20
)
{
    /// <summary>Default page size applied when none is specified by the caller.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum allowed page size to prevent unbounded queries.</summary>
    public const int MaxPageSize = 100;
}
