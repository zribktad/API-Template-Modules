using System.ComponentModel.DataAnnotations;
using APITemplate.Tests.Unit.Helpers;
using ProductCatalog.Features.Product.UpdateProducts;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

[Trait("Category", "Unit")]
public class UpdateProductRequestValidatorTests
{
    // --- Data Annotations ---

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

    // --- Cross-field Data Annotations ---

    [Theory]
    [InlineData(1001, null, false, "Description")]
    [InlineData(1001, "Detailed description", true, null)]
    [InlineData(999, null, true, null)]
    public void Annotation_DescriptionRule_BasedOnPrice(
        decimal price,
        string? description,
        bool expectedIsValid,
        string? expectedErrorProperty
    )
    {
        var request = new UpdateProductRequest("Any name", description, price);
        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBe(expectedIsValid);
        if (expectedErrorProperty is null)
        {
            results.ShouldNotContain(r => r.MemberNames.Contains("Description"));
        }
        else
        {
            results.ShouldContain(r => r.MemberNames.Contains(expectedErrorProperty));
        }
    }
}
