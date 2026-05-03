namespace BuildingBlocks.Application.Contracts;

/// <summary>
///     Marks a query/filter request as supporting pagination. Handlers and repositories consume this interface
///     without caring whether the implementation inherits <see cref="BuildingBlocks.Application.DTOs.PaginationFilter" />
///     or declares pagination fields inline (e.g. Wolverine HTTP filters that cannot inherit the base — see
///     <c>docs/validation.md</c>).
/// </summary>
public interface IPagedFilter
{
    /// <summary>1-based page index.</summary>
    public int PageNumber { get; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; }
}

