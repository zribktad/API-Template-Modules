using System.ComponentModel.DataAnnotations;
using APITemplate.Tests.Unit.Helpers;
using SharedKernel.Application.Validation;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Boundary;

public sealed class CollectionValidationAttributeTests
{
    private sealed record SampleRequest(
        [NoWhitespaceItems] [MaxLengthItems(5)] List<string>? Values
    );

    [Fact]
    public void NoWhitespaceItems_WhenCollectionValid_Passes()
    {
        var request = new SampleRequest(["abc", "de"]);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void NoWhitespaceItems_WhenCollectionContainsWhitespace_Fails()
    {
        var request = new SampleRequest(["abc", " "]);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.ErrorMessage == "Values must not contain null or empty values."
        );
        results.ShouldContain(r => r.MemberNames.Contains("Values"));
    }

    [Fact]
    public void MaxLengthItems_WhenCollectionContainsLongValue_Fails()
    {
        var request = new SampleRequest(["abcdef"]);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r =>
            r.ErrorMessage == "Values entries must not exceed 5 characters."
        );
        results.ShouldContain(r => r.MemberNames.Contains("Values"));
    }

    [Fact]
    public void CollectionAttributes_WhenCollectionNull_Pass()
    {
        var request = new SampleRequest(null);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    private sealed record SortRequest([SortDirection] string? SortDirection);

    [Theory]
    [InlineData("asc")]
    [InlineData("ASC")]
    [InlineData("desc")]
    [InlineData("DESC")]
    [InlineData(null)]
    public void SortDirectionAttribute_WhenValueValid_Passes(string? direction)
    {
        var request = new SortRequest(direction);

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void SortDirectionAttribute_WhenValueInvalid_Fails()
    {
        var request = new SortRequest("sideways");

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "SortDirection must be one of: asc, desc.");
    }
}
