using BuildingBlocks.Application.Batch;
using BuildingBlocks.Application.Batch.Rules;
using BuildingBlocks.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog;
using ProductCatalog.Features.Category.CreateCategories;
using ProductCatalog.Features.Category.UpdateCategories;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.Features.Product.UpdateProducts;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class ProductCatalogModuleTests
{
    /// <summary>
    ///     IBatchRule<T> is registered in the composition root (Program.cs via AddRequestValidation), not per-module.
    ///     This test verifies that once the generic registration is present, all ProductCatalog item types resolve.
    /// </summary>
    [Fact]
    public void DataAnnotationsBatchRule_ResolvesForProductCatalogBatchItemTypes()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IValidator, DataAnnotationsValidator>();
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
