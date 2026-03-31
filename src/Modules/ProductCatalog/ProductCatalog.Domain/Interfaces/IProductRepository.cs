using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Domain.Interfaces;

/// <summary>
/// Domain-facing repository contract for <see cref="Product"/> entities.
/// </summary>
public interface IProductRepository : IRepository<Product>;
