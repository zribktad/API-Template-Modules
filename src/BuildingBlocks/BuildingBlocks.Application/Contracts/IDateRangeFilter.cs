namespace BuildingBlocks.Application.Contracts;

/// <summary>
///     Marks a query/filter request as supporting optional creation-date range filtering.
///     Query handlers use this interface to apply a consistent date predicate without duplicating logic.
/// </summary>
public interface IDateRangeFilter
{
    /// <summary>Inclusive lower bound of the creation-date filter; <c>null</c> means no lower bound.</summary>
    DateTime? CreatedFrom { get; }

    DateTime? CreatedTo { get; }
}
