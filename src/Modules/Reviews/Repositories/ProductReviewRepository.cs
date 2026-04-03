using Reviews.Domain;
using Reviews.Persistence;

namespace Reviews.Repositories;

/// <summary>EF Core repository for <see cref="ProductReview"/>, inheriting all standard CRUD and specification query support from <see cref="RepositoryBase{T}"/>.</summary>
public sealed class ProductReviewRepository
    : RepositoryBase<ProductReview>,
        IProductReviewRepository
{
    public ProductReviewRepository(ReviewsDbContext dbContext)
        : base(dbContext) { }
}









