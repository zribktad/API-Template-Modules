using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Repositories;

/// <summary>EF Core repository for <see cref="ProductReview"/>, inheriting all standard CRUD and specification query support from <see cref="RepositoryBase{T}"/>.</summary>
public sealed class ProductReviewRepository
    : RepositoryBase<ProductReview>,
        IProductReviewRepository
{
    public ProductReviewRepository(ReviewsDbContext dbContext)
        : base(dbContext) { }
}


