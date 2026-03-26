using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Infrastructure.Persistence;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Tests.Integration.Helpers;

internal sealed class InMemoryProductRepository : IProductRepository
{
    private static readonly IReadOnlyList<ProductPriceFacetBucketResponse> DefaultPriceBuckets =
    [
        new("0 - 50", 0m, 50m, 0),
        new("50 - 100", 50m, 100m, 0),
        new("100 - 250", 100m, 250m, 0),
        new("250 - 500", 250m, 500m, 0),
        new("500+", 500m, null, 0),
    ];

    private readonly AppDbContext _dbContext;
    private readonly InnerRepository _inner;

    public InMemoryProductRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _inner = new InnerRepository(dbContext);
    }

    public async Task<PagedResponse<ProductResponse>> GetPagedAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        var specification = new ProductSpecification(filter);
        var items = await SpecificationEvaluator
            .Default.GetQuery(_dbContext.Products.AsQueryable(), specification)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var count = await SpecificationEvaluator
            .Default.GetQuery(
                _dbContext.Products.AsQueryable(),
                (ISpecification<ProductEntity>)specification
            )
            .CountAsync(ct);

        return new PagedResponse<ProductResponse>(items, count, filter.PageNumber, filter.PageSize);
    }

    public async Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        var specification = new ProductCategoryFacetSpecification(filter);
        var query = SpecificationEvaluator.Default.GetQuery(
            _dbContext.Products.AsQueryable(),
            specification
        );
        var products = await query.ToListAsync(ct);
        var categoryNames = await _dbContext
            .Categories.AsNoTracking()
            .ToDictionaryAsync(category => category.Id, category => category.Name, ct);

        return products
            .GroupBy(product => new
            {
                product.CategoryId,
                CategoryName = product.CategoryId.HasValue
                && categoryNames.TryGetValue(product.CategoryId.Value, out var categoryName)
                    ? categoryName
                    : "Uncategorized",
            })
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.CategoryName)
            .Select(group => new ProductCategoryFacetValue(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Count()
            ))
            .ToArray();
    }

    public async Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        var specification = new ProductPriceFacetSpecification(filter);
        var query = SpecificationEvaluator.Default.GetQuery(
            _dbContext.Products.AsQueryable(),
            specification
        );
        var products = await query.ToListAsync(ct);

        return DefaultPriceBuckets
            .Select(bucket =>
                bucket with
                {
                    Count = products.Count(product =>
                        product.Price >= bucket.MinPrice
                        && (bucket.MaxPrice is null || product.Price < bucket.MaxPrice.Value)
                    ),
                }
            )
            .ToArray();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default, string? errorCode = null)
    {
        var entity =
            await _inner.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(
                nameof(ProductEntity),
                id,
                errorCode ?? ErrorCatalog.General.NotFound
            );
        _dbContext.Set<ProductEntity>().Remove(entity);
    }

    public Task<ProductEntity?> GetByIdAsync<TId>(
        TId id,
        CancellationToken cancellationToken = default
    )
        where TId : notnull => _inner.GetByIdAsync(id, cancellationToken);

    public Task<ProductEntity?> FirstOrDefaultAsync(
        ISpecification<ProductEntity> specification,
        CancellationToken cancellationToken = default
    ) => _inner.FirstOrDefaultAsync(specification, cancellationToken);

    public Task<TResult?> FirstOrDefaultAsync<TResult>(
        ISpecification<ProductEntity, TResult> specification,
        CancellationToken cancellationToken = default
    ) => _inner.FirstOrDefaultAsync(specification, cancellationToken);

    public Task<ProductEntity?> SingleOrDefaultAsync(
        ISingleResultSpecification<ProductEntity> specification,
        CancellationToken cancellationToken = default
    ) => _inner.SingleOrDefaultAsync(specification, cancellationToken);

    public Task<TResult?> SingleOrDefaultAsync<TResult>(
        ISingleResultSpecification<ProductEntity, TResult> specification,
        CancellationToken cancellationToken = default
    ) => _inner.SingleOrDefaultAsync(specification, cancellationToken);

    public Task<List<ProductEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        _inner.ListAsync(cancellationToken);

    public Task<List<ProductEntity>> ListAsync(
        ISpecification<ProductEntity> specification,
        CancellationToken cancellationToken = default
    ) => _inner.ListAsync(specification, cancellationToken);

    public Task<List<TResult>> ListAsync<TResult>(
        ISpecification<ProductEntity, TResult> specification,
        CancellationToken cancellationToken = default
    ) => _inner.ListAsync(specification, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _inner.CountAsync(cancellationToken);

    public Task<int> CountAsync(
        ISpecification<ProductEntity> specification,
        CancellationToken cancellationToken = default
    ) => _inner.CountAsync(specification, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        _inner.AnyAsync(cancellationToken);

    public Task<bool> AnyAsync(
        ISpecification<ProductEntity> specification,
        CancellationToken cancellationToken = default
    ) => _inner.AnyAsync(specification, cancellationToken);

    public IAsyncEnumerable<ProductEntity> AsAsyncEnumerable(
        ISpecification<ProductEntity> specification
    ) => _inner.AsAsyncEnumerable(specification);

    public Task<ProductEntity> AddAsync(
        ProductEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        _dbContext.Set<ProductEntity>().Add(entity);
        return Task.FromResult(entity);
    }

    public Task<IEnumerable<ProductEntity>> AddRangeAsync(
        IEnumerable<ProductEntity> entities,
        CancellationToken cancellationToken = default
    ) => _inner.AddRangeAsync(entities, cancellationToken);

    public Task<int> UpdateAsync(
        ProductEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        _dbContext.Set<ProductEntity>().Update(entity);
        return Task.FromResult(0);
    }

    public Task<int> UpdateRangeAsync(
        IEnumerable<ProductEntity> entities,
        CancellationToken cancellationToken = default
    ) => _inner.UpdateRangeAsync(entities, cancellationToken);

    public Task<int> DeleteAsync(
        ProductEntity entity,
        CancellationToken cancellationToken = default
    ) => _inner.DeleteAsync(entity, cancellationToken);

    public Task<int> DeleteRangeAsync(
        IEnumerable<ProductEntity> entities,
        CancellationToken cancellationToken = default
    ) => _inner.DeleteRangeAsync(entities, cancellationToken);

    public Task<int> DeleteRangeAsync(
        ISpecification<ProductEntity> specification,
        CancellationToken cancellationToken = default
    ) => _inner.DeleteRangeAsync(specification, cancellationToken);

    public async Task<PagedResponse<TResult>> GetPagedAsync<TResult>(
        ISpecification<ProductEntity, TResult> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var count = await _inner.CountAsync(spec, ct);
        var items = await _inner.ListAsync(spec, ct);
        var pagedItems = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResponse<TResult>(pagedItems, count, pageNumber, pageSize);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _inner.SaveChangesAsync(cancellationToken);

    private sealed class InnerRepository : RepositoryBase<ProductEntity>
    {
        public InnerRepository(AppDbContext dbContext)
            : base(dbContext) { }
    }
}
