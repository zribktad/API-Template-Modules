namespace APITemplate.Tests.Integration.GraphQL;

public sealed record ProductItem(
    Guid Id,
    string Name,
    decimal Price,
    List<Guid>? ProductDataIds = null
);

public sealed record ProductCategoryFacetItem(Guid? CategoryId, string CategoryName, int Count);

public sealed record ProductPriceFacetBucketItem(
    string Label,
    decimal MinPrice,
    decimal? MaxPrice,
    int Count
);

public sealed record ProductFacets(
    List<ProductCategoryFacetItem> Categories,
    List<ProductPriceFacetBucketItem> PriceBuckets
);

public sealed record ProductResultsPage(
    List<ProductItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public sealed record ProductPage(ProductResultsPage Page, ProductFacets? Facets = null);

public sealed record ProductsData(ProductPage Products);

public sealed record ProductReviewNestedItem(Guid Id, int Rating, Guid ProductId);

public sealed record ProductWithReviewsItem(
    Guid Id,
    string Name,
    decimal Price,
    List<ProductReviewNestedItem> Reviews
);

public sealed record ProductWithReviewsResultsPage(
    List<ProductWithReviewsItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public sealed record ProductWithReviewsPage(
    ProductWithReviewsResultsPage Page,
    ProductFacets? Facets = null
);

public sealed record ProductsWithReviewsData(ProductWithReviewsPage Products);

public sealed record CategoryItem(Guid Id, string Name, string? Description);

public sealed record CategoryResultsPage(
    List<CategoryItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public sealed record CategoryPage(CategoryResultsPage Page);

public sealed record CategoriesData(CategoryPage Categories);

/// <summary>GraphQL shape of <see cref="BatchResultItem"/> (camelCase JSON).</summary>
public sealed record GraphQLBatchFailure(int Index, Guid? Id, List<string> Errors);

/// <summary>GraphQL shape of <see cref="BatchResponse"/>.</summary>
public sealed record GraphQLBatchResult(
    List<GraphQLBatchFailure> Failures,
    int SuccessCount,
    int FailureCount
);

public sealed record CreateProductsData(GraphQLBatchResult CreateProducts);

public sealed record ProductByIdData(ProductItem? ProductById);

public sealed record DeleteProductData(bool DeleteProduct);

public sealed record DeleteProductsData(GraphQLBatchResult DeleteProducts);
