using APITemplate.Tests.Unit.Helpers;
using ProductCatalog.Features.Product.CreateProducts;
using Reviews.Features;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Boundary;

public sealed class ValidationAttributeTests
{
    [Fact]
    public void GreaterThanOrEqualToProperty_WhenBothValuesAreNull_Passes()
    {
        ProductReviewFilter filter = new();

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void GreaterThanOrEqualToProperty_WhenOnlyLowerBoundExists_Passes()
    {
        ProductReviewFilter filter = new(MinRating: 3, MaxRating: null);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void GreaterThanOrEqualToProperty_WhenRangeIsReversed_FailsWithMemberName()
    {
        ProductReviewFilter filter = new(MinRating: 5, MaxRating: 1);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains("MaxRating")
            && r.ErrorMessage == "MaxRating must be greater than or equal to MinRating."
        );
    }

    [Theory]
    [InlineData(1001, null, false)]
    [InlineData(1001, "", false)]
    [InlineData(1001, "   ", false)]
    [InlineData(1001, "Detailed description", true)]
    [InlineData(999, null, true)]
    public void RequiredWhenDecimalPropertyExceeds_EnforcesThresholdRule(
        decimal price,
        string? description,
        bool expectedIsValid
    )
    {
        CreateProductRequest request = new("Product", description, price);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBe(expectedIsValid);
        if (expectedIsValid)
        {
            results.ShouldNotContain(r => r.MemberNames.Contains("Description"));
        }
        else
        {
            results.ShouldContain(r =>
                r.MemberNames.Contains("Description")
                && r.ErrorMessage == "Description is required for products priced above 1000."
            );
        }
    }
}
