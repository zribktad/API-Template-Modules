namespace ProductCatalog.Features.Shared.Routing;

/// <summary>Shared path fragments for ProductCatalog where not implied by <c>[controller]</c>.</summary>
public static class ProductCatalogRouteTemplates
{
    /// <summary>Path segment for 201 <c>Location</c> on idempotent POST (matches kebab-case <c>[controller]</c> URL).</summary>
    public const string IdempotentPathSegment = "idempotent";
}
