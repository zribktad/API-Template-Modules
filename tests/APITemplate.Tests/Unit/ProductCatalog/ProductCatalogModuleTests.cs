using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog;
using ProductCatalog.Features.Category.CreateCategories;
using ProductCatalog.Features.Category.UpdateCategories;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.Features.Product.UpdateProducts;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductCatalogModuleTests
{
    /// <summary>
    ///     IBatchRule&lt;T&gt; is registered in the composition root (AddApiFoundation), not per-module.
    ///     This test verifies that once the generic registration is present, all ProductCatalog item types resolve.
    /// </summary>
    [Fact]
    public void DataAnnotationsBatchRule_ResolvesForProductCatalogBatchItemTypes()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped(typeof(IBatchRule<>), typeof(DataAnnotationsBatchRule<>));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        AssertBatchRuleRegistered<CreateProductRequest>(scope);
        AssertBatchRuleRegistered<UpdateProductItem>(scope);
        AssertBatchRuleRegistered<CreateCategoryRequest>(scope);
        AssertBatchRuleRegistered<UpdateCategoryItem>(scope);
    }

    private static void AssertBatchRuleRegistered<T>(IServiceScope scope)
    {
        scope
            .ServiceProvider.GetRequiredService<IBatchRule<T>>()
            .ShouldBeOfType<DataAnnotationsBatchRule<T>>();
    }
}
