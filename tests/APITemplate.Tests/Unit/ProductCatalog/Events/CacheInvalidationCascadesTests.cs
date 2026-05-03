using ProductCatalog;
using ProductCatalog.Common.Events;
using SharedKernel.Contracts.Events;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog.Events;

[Trait("Category", "Unit")]
public sealed class CacheInvalidationCascadesTests
{
    [Fact]
    public void ForProductChange_ShouldContainProductsAndCategories()
    {
        CacheInvalidationCascades
            .ForProductChange.Select(x => x.CacheTag)
            .ShouldBe([CacheTags.Products, CacheTags.Categories], ignoreOrder: true);
    }

    [Fact]
    public void ForProductDeletion_ShouldContainProductsCategoriesAndReviews()
    {
        CacheInvalidationCascades
            .ForProductDeletion.Select(x => x.CacheTag)
            .ShouldBe(
                [CacheTags.Products, CacheTags.Categories, CrossModuleCacheTags.Reviews],
                ignoreOrder: true
            );
    }

    [Fact]
    public void ForCategoryDeletion_ShouldContainCategoriesAndProducts()
    {
        CacheInvalidationCascades
            .ForCategoryDeletion.Select(x => x.CacheTag)
            .ShouldBe([CacheTags.Categories, CacheTags.Products], ignoreOrder: true);
    }

    [Fact]
    public void ForProductDataDeletion_ShouldContainProductDataAndProducts()
    {
        CacheInvalidationCascades
            .ForProductDataDeletion.Select(x => x.CacheTag)
            .ShouldBe([CacheTags.ProductData, CacheTags.Products], ignoreOrder: true);
    }
}
