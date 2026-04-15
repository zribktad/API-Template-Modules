using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Features.Tenant.DTOs;
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

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(
            r => r.ErrorMessage == "SortBy must be one of: Name, Price, CreatedAt."
        );
    }

    [Fact]
    public void ProductFilter_WhenPriceRangeInverted_FailsValidation()
    {
        var filter = new ProductFilter(MinPrice: 10m, MaxPrice: 5m);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(
            r => r.ErrorMessage == "MaxPrice must be greater than or equal to MinPrice."
        );
    }

    [Fact]
    public void ProductReviewFilter_WhenRatingRangeInvalid_FailsValidation()
    {
        var filter = new ProductReviewFilter(MinRating: 5, MaxRating: 1);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(
            r => r.ErrorMessage == "MaxRating must be greater than or equal to MinRating."
        );
    }

    [Fact]
    public void ProductReviewFilter_WhenMinRatingOutOfRange_FailsValidation()
    {
        var filter = new ProductReviewFilter(MinRating: 0);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "MinRating must be between 1 and 5.");
    }

    [Fact]
    public void TenantFilter_WhenSortDirectionInvalid_FailsValidation()
    {
        var filter = new TenantFilter(SortBy: "Code", SortDirection: "sideways");

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "SortDirection must be one of: asc, desc.");
    }

    [Fact]
    public void ProductFilter_WhenCategoryIdsContainEmptyGuid_FailsValidation()
    {
        var filter = new ProductFilter(CategoryIds: new[] { Guid.Empty });

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "CategoryIds cannot contain an empty value.");
    }

    [Fact]
    public void CategoryFilter_WhenSortByAllowed_PassesValidation()
    {
        var filter = new global::ProductCatalog.Features.Category.GetCategories.CategoryFilter(
            SortBy: "Name",
            SortDirection: "asc"
        );

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }
}
