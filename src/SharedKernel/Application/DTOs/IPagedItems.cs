using SharedKernel.Domain.Common;

namespace SharedKernel.Application.DTOs;

/// <summary>
/// Marks a query response as wrapping a <see cref="PagedResponse{T}"/>, providing a consistent
/// shape for all paginated query results across the Application layer.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public interface IPagedItems<T>
{
    PagedResponse<T> Page { get; }
}
