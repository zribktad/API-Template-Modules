namespace ProductCatalog.Features.Product.Shared;

/// <summary>
///     Shared contract for create and update product command requests, enabling reuse of
///     FluentValidation rules across both operations without duplicating property declarations.
/// </summary>
public interface IProductRequest
{
    public string Name { get; }
    public string? Description { get; }
    public decimal Price { get; }
    public Guid? CategoryId { get; }
    public IReadOnlyCollection<Guid>? ProductDataIds { get; }
}
