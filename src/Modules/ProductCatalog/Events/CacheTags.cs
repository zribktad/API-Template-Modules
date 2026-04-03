namespace ProductCatalog.Events;

public static class CacheTags
{
    public const string Products = "Products";
    public const string Categories = "Categories";
    public const string ProductData = "ProductData";
    /// <summary>Reviews cache is also invalidated when products are deleted (orphaned reviews).</summary>
    public const string Reviews = "Reviews";
}

