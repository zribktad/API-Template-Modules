using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>EF Core repository for <see cref="ProductReview"/>, inheriting all standard CRUD and specification query support from <see cref="RepositoryBase{T}"/>.</summary>
public sealed class ProductReviewRepository
    : RepositoryBase<ProductReview>,
        IProductReviewRepository
{
    public ProductReviewRepository(AppDbContext dbContext)
        : base(dbContext) { }
}
