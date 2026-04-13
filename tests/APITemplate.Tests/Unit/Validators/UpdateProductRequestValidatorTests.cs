using System.ComponentModel.DataAnnotations;
using APITemplate.Tests.Unit.Helpers;
using ProductCatalog.Features.Product.UpdateProducts;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

public class UpdateProductRequestValidatorTests
{
    private readonly UpdateProductRequestValidator _sut = new();

    // --- Data Annotations — same as ASP.NET model validation; not duplicated in batch FV ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Annotation_InvalidName_IsInvalid(string? name)
    {
        var request = new UpdateProductRequest(name!, null, 19.99m);

        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Name"));
    }

    [Fact]
    public void Annotation_NameExceeds200Characters_IsInvalid()
    {
        var request = new UpdateProductRequest(new string('A', 201), null, 19.99m);

        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Name"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50.25)]
    public void Annotation_PriceNegative_IsInvalid(decimal price)
    {
        var request = new UpdateProductRequest("Valid Name", null, price);

        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Price"));
    }

    [Fact]
    public void Annotation_PriceZero_IsValid()
    {
        var request = new UpdateProductRequest("Valid Name", null, 0m);

        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBeTrue();
        results.ShouldNotContain(r => r.MemberNames.Contains("Price"));
    }

    // --- FluentValidation (cross-field rules only) ---

    [Theory]
    [InlineData(1001, null, false, "Description")]
    [InlineData(1001, "Detailed description", true, null)]
    [InlineData(999, null, true, null)]
    public void FluentValidation_DescriptionRule_BasedOnPrice(
        decimal price,
        string? description,
        bool expectedIsValid,
        string? expectedErrorProperty
    )
    {
        var result = _sut.Validate(new UpdateProductRequest("Any name", description, price));

        result.IsValid.ShouldBe(expectedIsValid);
        if (expectedErrorProperty is null)
        {
            result.Errors.ShouldNotContain(e => e.PropertyName == "Description");
        }
        else
        {
            result.Errors.ShouldContain(e => e.PropertyName == expectedErrorProperty);
        }
    }
}
