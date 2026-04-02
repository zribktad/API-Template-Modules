using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Api;
using ProductCatalog.Application.Features.Category.DTOs;
using ProductCatalog.Application.Features.Product.DTOs;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductCatalogModuleTests
{
    [Fact]
    public void AddProductCatalogModule_RegistersFluentValidationBatchRulesForBatchItemTypes()
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

        scope
            .ServiceProvider.GetRequiredService<IBatchRule<CreateProductRequest>>()
            .ShouldBeOfType<FluentValidationBatchRule<CreateProductRequest>>();
        scope
            .ServiceProvider.GetRequiredService<IBatchRule<UpdateProductItem>>()
            .ShouldBeOfType<FluentValidationBatchRule<UpdateProductItem>>();
        scope
            .ServiceProvider.GetRequiredService<IBatchRule<CreateCategoryRequest>>()
            .ShouldBeOfType<FluentValidationBatchRule<CreateCategoryRequest>>();
        scope
            .ServiceProvider.GetRequiredService<IBatchRule<UpdateCategoryItem>>()
            .ShouldBeOfType<FluentValidationBatchRule<UpdateCategoryItem>>();

        scope
            .ServiceProvider.GetRequiredService<IValidator<CreateProductRequest>>()
            .ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IValidator<UpdateProductItem>>().ShouldNotBeNull();
        scope
            .ServiceProvider.GetRequiredService<IValidator<CreateCategoryRequest>>()
            .ShouldNotBeNull();
        scope
            .ServiceProvider.GetRequiredService<IValidator<UpdateCategoryItem>>()
            .ShouldNotBeNull();
    }
}
