namespace APITemplate.Domain.Common;

/// <summary>
/// Generic paged result envelope returned by list queries throughout the Application layer.
/// Carries the current page of items together with metadata needed for client-side pagination controls.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    /// <summary>Total number of pages derived from <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Returns <c>true</c> when a previous page exists.</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Returns <c>true</c> when a subsequent page exists.</summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
