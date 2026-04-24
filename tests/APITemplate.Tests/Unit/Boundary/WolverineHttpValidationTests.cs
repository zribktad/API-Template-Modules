using System.ComponentModel.DataAnnotations;
using Reviews.Features;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Boundary;

/// <summary>
///     Parametric edge-case tests that mirror exactly what Wolverine HTTP's
///     <c>DataAnnotationsHttpValidationExecutor</c> does at request time:
///     <c>Validator.TryValidateObject(..., validateAllProperties: true)</c>.
///     If these pass, Wolverine HTTP will return 400 ProblemDetails; if they fail,
///     the request escapes validation and likely 500s downstream.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WolverineHttpValidationTests
{
    private static bool TryValidateLikeWolverineHttp(
        object instance,
        out List<ValidationResult> results
    )
    {
        results = [];
        return Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            validateAllProperties: true
        );
    }

    // -------------------- CreateProductReviewRequest --------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void CreateProductReviewRequest_WhenRatingOutOfRange_FailsValidation(int rating)
    {
        CreateProductReviewRequest request = new() { ProductId = Guid.NewGuid(), Rating = rating };

        bool isValid = TryValidateLikeWolverineHttp(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(CreateProductReviewRequest.Rating))
            && r.ErrorMessage == "Rating must be between 1 and 5."
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void CreateProductReviewRequest_WhenRatingInRange_PassesValidation(int rating)
    {
        CreateProductReviewRequest request = new() { ProductId = Guid.NewGuid(), Rating = rating };

        bool isValid = TryValidateLikeWolverineHttp(request, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void CreateProductReviewRequest_WhenProductIdIsEmpty_FailsValidation()
    {
        CreateProductReviewRequest request = new() { ProductId = Guid.Empty, Rating = 3 };

        bool isValid = TryValidateLikeWolverineHttp(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(CreateProductReviewRequest.ProductId))
            && r.ErrorMessage == "ProductId is required."
        );
    }

    [Fact]
    public void CreateProductReviewRequest_WhenMultipleErrors_AllReported()
    {
        CreateProductReviewRequest request = new() { ProductId = Guid.Empty, Rating = 0 };

        bool isValid = TryValidateLikeWolverineHttp(request, out var results);

        isValid.ShouldBeFalse();
        results.Count.ShouldBe(2);
        results.ShouldContain(r => r.ErrorMessage == "ProductId is required.");
        results.ShouldContain(r => r.ErrorMessage == "Rating must be between 1 and 5.");
    }

    [Fact]
    public void CreateProductReviewRequest_WhenCommentIsNull_PassesValidation()
    {
        CreateProductReviewRequest request = new()
        {
            ProductId = Guid.NewGuid(),
            Comment = null,
            Rating = 4,
        };

        bool isValid = TryValidateLikeWolverineHttp(request, out _);

        isValid.ShouldBeTrue();
    }

    // -------------------- ProductReviewFilter --------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public void ProductReviewFilter_WhenMinRatingOutOfRange_FailsValidation(int minRating)
    {
        var filter = new ProductReviewFilter(MinRating: minRating);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(ProductReviewFilter.MinRating))
            && r.ErrorMessage == "MinRating must be between 1 and 5."
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(99)]
    public void ProductReviewFilter_WhenMaxRatingOutOfRange_FailsValidation(int maxRating)
    {
        var filter = new ProductReviewFilter(MaxRating: maxRating);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(ProductReviewFilter.MaxRating))
            && r.ErrorMessage == "MaxRating must be between 1 and 5."
        );
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(4, 2)]
    [InlineData(3, 2)]
    public void ProductReviewFilter_WhenRatingRangeInverted_FailsValidation(int min, int max)
    {
        var filter = new ProductReviewFilter(MinRating: min, MaxRating: max);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(ProductReviewFilter.MaxRating))
            && r.ErrorMessage == "MaxRating must be greater than or equal to MinRating."
        );
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(3, 5)]
    public void ProductReviewFilter_WhenRatingRangeValid_PassesValidation(int min, int max)
    {
        var filter = new ProductReviewFilter(MinRating: min, MaxRating: max);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void ProductReviewFilter_WhenDateRangeInverted_FailsValidation()
    {
        var filter = new ProductReviewFilter(
            CreatedFrom: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedTo: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(ProductReviewFilter.CreatedTo))
            && r.ErrorMessage == "CreatedTo must be greater than or equal to CreatedFrom."
        );
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("title")]
    [InlineData("")]
    public void ProductReviewFilter_WhenSortByUnknown_FailsValidation(string sortBy)
    {
        var filter = new ProductReviewFilter(SortBy: sortBy);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(ProductReviewFilter.SortBy))
            && r.ErrorMessage == "SortBy must be one of: Rating, CreatedAt."
        );
    }

    [Theory]
    [InlineData("Rating")]
    [InlineData("rating")]
    [InlineData("RATING")]
    [InlineData("CreatedAt")]
    [InlineData("createdat")]
    public void ProductReviewFilter_WhenSortByAllowed_PassesValidation(string sortBy)
    {
        var filter = new ProductReviewFilter(SortBy: sortBy);

        bool isValid = TryValidateLikeWolverineHttp(filter, out _);

        isValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("up")]
    [InlineData("sideways")]
    [InlineData("ascending")]
    public void ProductReviewFilter_WhenSortDirectionInvalid_FailsValidation(string sortDirection)
    {
        var filter = new ProductReviewFilter(SortDirection: sortDirection);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.MemberNames.Contains(nameof(ProductReviewFilter.SortDirection))
            && r.ErrorMessage == "SortDirection must be one of: asc, desc."
        );
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    [InlineData("ASC")]
    [InlineData("Desc")]
    public void ProductReviewFilter_WhenSortDirectionValid_PassesValidation(string sortDirection)
    {
        var filter = new ProductReviewFilter(SortDirection: sortDirection);

        bool isValid = TryValidateLikeWolverineHttp(filter, out _);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ProductReviewFilter_WhenAllFieldsDefault_PassesValidation()
    {
        var filter = new ProductReviewFilter();

        bool isValid = TryValidateLikeWolverineHttp(filter, out _);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ProductReviewFilter_WhenMultipleErrors_AllReported()
    {
        var filter = new ProductReviewFilter(
            MinRating: 0,
            MaxRating: 6,
            SortBy: "nope",
            SortDirection: "up"
        );

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "MinRating must be between 1 and 5.");
        results.ShouldContain(r => r.ErrorMessage == "MaxRating must be between 1 and 5.");
        results.ShouldContain(r => r.ErrorMessage == "SortBy must be one of: Rating, CreatedAt.");
        results.ShouldContain(r => r.ErrorMessage == "SortDirection must be one of: asc, desc.");
    }

    // -------------------- Pagination --------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ProductReviewFilter_WhenPageNumberInvalid_FailsValidation(int pageNumber)
    {
        var filter = new ProductReviewFilter(PageNumber: pageNumber);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.ErrorMessage == "PageNumber must be greater than or equal to 1."
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(1_000)]
    public void ProductReviewFilter_WhenPageSizeInvalid_FailsValidation(int pageSize)
    {
        var filter = new ProductReviewFilter(PageSize: pageSize);

        bool isValid = TryValidateLikeWolverineHttp(filter, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "PageSize must be between 1 and 100.");
    }
}
