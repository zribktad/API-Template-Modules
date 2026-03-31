namespace SharedKernel.Application.DTOs;

/// <summary>
/// Marks a query response as carrying faceted aggregation data alongside the primary result set,
/// enabling clients to render filter counts or category breakdowns without an extra round-trip.
/// </summary>
/// <typeparam name="TFacets">The type that holds the facet aggregations specific to the query.</typeparam>
public interface IHasFacets<TFacets>
{
    TFacets Facets { get; }
}
