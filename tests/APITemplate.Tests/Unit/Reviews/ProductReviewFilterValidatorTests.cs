using FluentValidation.Results;
using Reviews.Features;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Reviews;

public sealed class ProductReviewFilterValidatorTests
{
    private readonly ProductReviewFilterValidator _sut = new();

    [Theory]
    [InlineData(0, 5)]
    [InlineData(6, 5)]
    [InlineData(2, 1)]
    public void Validate_WhenRatingRangeInvalid_Fails(int min, int max)
    {
        ProductReviewFilter filter = new(MinRating: min, MaxRating: max);

        ValidationResult result = _sut.Validate(filter);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(5, 5)]
    public void Validate_WhenRatingRangeValid_Passes(int min, int max)
    {
        ProductReviewFilter filter = new(MinRating: min, MaxRating: max);

        ValidationResult result = _sut.Validate(filter);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-a-field")]
    [InlineData("productId")]
    public void Validate_WhenSortByUnknown_Fails(string sortBy)
    {
        ProductReviewFilter filter = new(SortBy: sortBy, SortDirection: "asc");

        ValidationResult result = _sut.Validate(filter);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("Rating")]
    [InlineData("createdAt")]
    [InlineData("CREATEDAT")]
    public void Validate_WhenSortByAllowed_Passes(string sortBy)
    {
        ProductReviewFilter filter = new(SortBy: sortBy, SortDirection: "desc");

        ValidationResult result = _sut.Validate(filter);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("up")]
    [InlineData("")]
    public void Validate_WhenSortDirectionInvalid_Fails(string direction)
    {
        ProductReviewFilter filter = new(SortBy: "rating", SortDirection: direction);

        ValidationResult result = _sut.Validate(filter);

        result.IsValid.ShouldBeFalse();
    }
}
