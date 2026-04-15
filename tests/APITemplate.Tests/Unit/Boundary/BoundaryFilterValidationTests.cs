using Identity.Directory.Features.Tenant.Validation;
using Identity.Directory.Features.Tenant.DTOs;
using ProductCatalog.Features.Category.GetCategories;
using ProductCatalog.Features.Product.GetProducts;
using Reviews.Features;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Boundary;

public sealed class BoundaryFilterValidationTests
{
    [Fact]
    public void ProductFilter_WhenSortByUnknown_FailsValidation()
    {
        var filter = new ProductFilter(SortBy: "nope", SortDirection: "asc");
        var validator = new ProductFilterValidator();

        var results = validator.Validate(filter);

        results.IsValid.ShouldBeFalse();
        results.Errors.ShouldContain(
            r => r.ErrorMessage == "SortBy must be one of: name, price, createdAt."
        );
    }

    [Fact]
    public void ProductFilter_WhenPriceRangeInverted_FailsValidation()
    {
        var filter = new ProductFilter(MinPrice: 10m, MaxPrice: 5m);
        var validator = new ProductFilterValidator();

        var results = validator.Validate(filter);

        results.IsValid.ShouldBeFalse();
        results.Errors.ShouldContain(
            r => r.ErrorMessage == "MaxPrice must be greater than or equal to MinPrice."
        );
    }

    [Fact]
    public void ProductReviewFilter_WhenRatingRangeInvalid_FailsValidation()
    {
        var filter = new ProductReviewFilter(MinRating: 5, MaxRating: 1);
        var validator = new ProductReviewFilterValidator();

        var results = validator.Validate(filter);

        results.IsValid.ShouldBeFalse();
        results.Errors.ShouldContain(
            r => r.ErrorMessage == "MaxRating must be greater than or equal to MinRating."
        );
    }

    [Fact]
    public void TenantFilter_WhenSortDirectionInvalid_FailsValidation()
    {
        var filter = new TenantFilter(SortBy: "code", SortDirection: "sideways");
        var validator = new TenantFilterValidator();

        var results = validator.Validate(filter);

        results.IsValid.ShouldBeFalse();
        results.Errors.ShouldContain(r => r.ErrorMessage == "SortDirection must be one of: asc, desc.");
    }

    [Fact]
    public void CategoryFilter_WhenSortByAllowed_PassesValidation()
    {
        var filter = new CategoryFilter(SortBy: "name", SortDirection: "asc");
        var validator = new CategoryFilterValidator();

        var results = validator.Validate(filter);

        results.IsValid.ShouldBeTrue();
    }
}
