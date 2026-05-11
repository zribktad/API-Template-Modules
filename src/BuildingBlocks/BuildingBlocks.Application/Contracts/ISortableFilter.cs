namespace BuildingBlocks.Application.Contracts;

/// <summary>
///     Marks a query/filter request as supporting optional sorting parameters.
///     Query handlers use this interface to apply a consistent ordering strategy without duplicating logic.
/// </summary>
public interface ISortableFilter
{
    /// <summary>Name of the field to sort by; <c>null</c> applies default ordering.</summary>
    string? SortBy { get; }

    string? SortDirection { get; }
}
