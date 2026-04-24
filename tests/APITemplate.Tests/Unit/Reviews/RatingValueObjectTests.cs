using ErrorOr;
using Reviews.Domain;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Reviews;

[Trait("Category", "Unit")]
public sealed class RatingValueObjectTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MinValue)]
    public void Create_WhenOutOfRange_ReturnsError(int value)
    {
        ErrorOr<Rating> result = Rating.Create(value);

        result.IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Create_WhenInRange_ReturnsRating(int value)
    {
        ErrorOr<Rating> result = Rating.Create(value);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(value);
    }

    [Fact]
    public void FromPersistence_DoesNotValidateRange()
    {
        Rating r = Rating.FromPersistence(99);

        r.Value.ShouldBe(99);
    }
}
