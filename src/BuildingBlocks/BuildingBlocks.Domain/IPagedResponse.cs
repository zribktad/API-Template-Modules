namespace BuildingBlocks.Domain.Common;

/// <summary>
///     Non-generic interface for <see cref="PagedResponse{T}"/> to allow identifying paginated
///     responses and accessing pagination metadata without reflection.
/// </summary>
public interface IPagedResponse
{
    int TotalCount { get; }
    int PageNumber { get; }
    int PageSize { get; }
    int TotalPages { get; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
}

