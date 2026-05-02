namespace SharedKernel.Contracts.Events;

/// <summary>
///     Centralized cache tag constants shared across modules to prevent duplicate definitions
///     and ensure consistent invalidation semantics.
/// </summary>
public static class CacheTags
{
    // ── ProductCatalog ────────────────────────────────────────────────────────
    public const string Products = "Products";
    public const string Categories = "Categories";
    public const string ProductData = "ProductData";

    // ── Reviews ───────────────────────────────────────────────────────────────
    public const string Reviews = "Reviews";

    // ── Identity ──────────────────────────────────────────────────────────────
    public const string Tenants = "Tenants";
    public const string TenantInvitations = "TenantInvitations";
    public const string Users = "Users";
}
