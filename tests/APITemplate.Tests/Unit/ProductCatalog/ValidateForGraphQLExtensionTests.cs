using HotChocolate;
using ProductCatalog.Features.Category.GetCategories;
using ProductCatalog.Features.Product.GetProducts;
using ProductCatalog.GraphQL;
using SharedKernel.Application.Validation;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ValidateForGraphQLExtensionTests
{
    private static readonly IValidator _validator = new DataAnnotationsValidator();

    [Fact]
    public void ValidateForGraphQL_ValidModel_DoesNotThrow()
    {
        ProductFilter filter = new(MinPrice: 0m, MaxPrice: 100m, SortBy: "price", SortDirection: "asc");

        Should.NotThrow(() => _validator.ValidateForGraphQL(filter));
    }

    [Fact]
    public void ValidateForGraphQL_SingleInvalidField_ThrowsGraphQLExceptionWithGEN0400()
    {
        ProductFilter filter = new(MinPrice: -1m);

        GraphQLException ex = Should.Throw<GraphQLException>(() => _validator.ValidateForGraphQL(filter));

        ex.Errors.Count.ShouldBe(1);
        ex.Errors[0].Code.ShouldBe("GEN-0400");
        ex.Errors[0].Message.ShouldContain("MinPrice");
    }

    [Fact]
    public void ValidateForGraphQL_MultipleInvalidFields_ThrowsOneErrorPerFailure()
    {
        ProductFilter filter = new(MinPrice: -1m, SortBy: "not-a-field");

        GraphQLException ex = Should.Throw<GraphQLException>(() => _validator.ValidateForGraphQL(filter));

        ex.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
        ex.Errors.ShouldAllBe(e => e.Code == "GEN-0400");
    }

    [Fact]
    public void ValidateForGraphQL_InvalidField_SetsPropertyNameExtension()
    {
        CategoryFilter filter = new(SortBy: "invalid-sort");

        GraphQLException ex = Should.Throw<GraphQLException>(() => _validator.ValidateForGraphQL(filter));

        IError error = ex.Errors[0];
        error.Code.ShouldBe("GEN-0400");
        error.Extensions.ShouldNotBeNull();
        error.Extensions!.ContainsKey("propertyName").ShouldBeTrue();
    }

    [Fact]
    public void ValidateForGraphQL_InvalidMaxLessThanMin_ThrowsWithGEN0400()
    {
        ProductFilter filter = new(MinPrice: 100m, MaxPrice: 50m);

        GraphQLException ex = Should.Throw<GraphQLException>(() => _validator.ValidateForGraphQL(filter));

        ex.Errors.ShouldContain(e => e.Code == "GEN-0400");
    }
}
