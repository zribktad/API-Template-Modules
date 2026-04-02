namespace APITemplate.Domain.Entities;

/// <summary>
/// Core domain entity representing a product in the catalog.
/// This is the aggregate root - all business rules around products start here.
/// </summary>
public sealed class Product : IAuditableTenantEntity, IHasId
{
    /// <summary>Unique identifier generated when the product is created.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the product. Required, max 200 characters (enforced by EF config + FluentValidation).</summary>
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
    public decimal Price
    {
        get => field;
        set =>
            field =
                value >= 0
                    ? value
                    : throw new ArgumentOutOfRangeException(
                        nameof(Price),
                        "Price must be greater than or equal to zero."
                    );
    }

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductDataLink> ProductDataLinks { get; set; } = [];

    public ICollection<ProductReview> Reviews { get; set; } = [];

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// Atomically replaces all mutable product fields in a single call, enforcing property-level invariants.
    /// </summary>
    public void UpdateDetails(string name, string? description, decimal price, Guid? categoryId)
    {
        Name = name;
        Description = description;
        Price = price;
        CategoryId = categoryId;
    }

    /// <summary>
    /// Reconciles the product's <see cref="ProductDataLinks"/> collection against the desired set of <paramref name="targetIds"/>.
    /// Removes links not in the target set and creates new links as needed.
    /// </summary>
    public void SyncProductDataLinks(
        HashSet<Guid> targetIds,
        Dictionary<Guid, ProductDataLink> existingById
    )
    {
        foreach (
            var link in ProductDataLinks
                .Where(link => !targetIds.Contains(link.ProductDataId))
                .ToArray()
        )
            ProductDataLinks.Remove(link);

        foreach (var productDataId in targetIds)
        {
            if (!existingById.ContainsKey(productDataId))
            {
                ProductDataLinks.Add(ProductDataLink.Create(Id, productDataId));
            }
        }
    }

    /// <summary>
    /// Removes all current product data links from the in-memory collection, preparing them for soft-delete by the persistence layer.
    /// </summary>
    public void SoftDeleteProductDataLinks()
    {
        foreach (var link in ProductDataLinks.ToArray())
            ProductDataLinks.Remove(link);
    }
}
