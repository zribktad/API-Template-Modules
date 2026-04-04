namespace Reviews.Domain;

/// <summary>
///     Repository contract for <see cref="ProductReview" /> entities, inheriting all generic CRUD operations from
///     <see cref="IRepository{T}" />.
/// </summary>
public interface IProductReviewRepository : IRepository<ProductReview> { }
