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
    [Fact]
    public void AddProductCatalogModule_RegistersDataAnnotationsBatchRulesForBatchItemTypes()
    {
        ServiceCollection services = new();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                }
            )
            .Build();

        services.AddProductCatalogModule(configuration);

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
