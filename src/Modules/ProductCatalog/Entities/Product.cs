using ProductCatalog.ValueObjects;

namespace ProductCatalog.Entities;

/// <summary>
///     Core domain entity representing a product in the catalog.
///     This is the aggregate root - all business rules around products start here.
/// </summary>
public sealed class Product : IAuditableTenantEntity, IHasId
{
    /// <summary>Display name of the product. Required, max 200 characters (enforced by EF config + request validation).</summary>
    public required string Name
    {
        get => field;
        set =>
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Product name cannot be empty.", nameof(Name))
                : value.Trim();
    }

    /// <summary>Optional longer description of the product.</summary>
    public string? Description { get; set; }

    /// <summary>Price with 18,2 decimal precision (enforced by EF config).</summary>
    public Price Price { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary>Infrastructure-only navigation for query projections. Domain logic must use <see cref="CategoryId" /> instead.</summary>
    public Category? Category { get; set; }

    public ICollection<ProductDataLink> ProductDataLinks { get; set; } = [];

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Unique identifier generated when the product is created.</summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Creates a new <see cref="Product" /> with the given fields and optional product-data links.
    /// </summary>
    public static Product Create(
        string name,
        string? description,
        Price price,
        Guid? categoryId,
        IEnumerable<Guid>? productDataIds
    )
    {
        Guid id = Guid.NewGuid();
        Product product = new()
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            CategoryId = categoryId,
        };
        if (productDataIds is not null)
        {
            foreach (Guid pdId in productDataIds.Distinct())
                product.ProductDataLinks.Add(ProductDataLink.Create(id, pdId));
        }

        return product;
    }

    /// <summary>
    ///     Atomically replaces all mutable product fields in a single call, enforcing property-level invariants.
    /// </summary>
    public void UpdateDetails(string name, string? description, Price price, Guid? categoryId)
    {
        Name = name;
        Description = description;
        Price = price;
        CategoryId = categoryId;
    }

    /// <summary>
    ///     Reconciles the product's <see cref="ProductDataLinks" /> collection against the desired set of
    ///     <paramref name="targetIds" />.
    ///     Removes links not in the target set and creates new links as needed.
    /// </summary>
    public void SyncProductDataLinks(IEnumerable<Guid> targetIds)
    {
        HashSet<Guid> targetSet = targetIds.ToHashSet();
        Dictionary<Guid, ProductDataLink> existingById = ProductDataLinks
            .GroupBy(link => link.ProductDataId)
            .ToDictionary(group => group.Key, group => group.First());

        ProductDataLink[] linksToRemove = ProductDataLinks
            .Where(link => !targetSet.Contains(link.ProductDataId))
            .ToArray();

        foreach (ProductDataLink link in linksToRemove)
            ProductDataLinks.Remove(link);

        foreach (Guid productDataId in targetSet)
        {
            if (!existingById.TryGetValue(productDataId, out ProductDataLink? existingLink))
            {
                ProductDataLinks.Add(ProductDataLink.Create(Id, productDataId));
                continue;
            }

            if (existingLink.IsDeleted)
                existingLink.Restore();
        }
    }

    /// <summary>
    ///     Removes all current product data links from the in-memory collection, preparing them for soft-delete by the
    ///     persistence layer.
    /// </summary>
    public void SoftDeleteProductDataLinks()
    {
        foreach (ProductDataLink? link in ProductDataLinks.ToArray())
            ProductDataLinks.Remove(link);
    }
}
