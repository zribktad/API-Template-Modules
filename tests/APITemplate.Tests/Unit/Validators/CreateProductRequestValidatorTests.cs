using System.ComponentModel.DataAnnotations;
using APITemplate.Tests.Unit.Helpers;
using APITemplate.Tests.Unit.TestData;
using ProductCatalog.Features.Product.CreateProducts;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

[Trait("Category", "Unit")]
public class CreateProductRequestValidatorTests
{
    // --- Data Annotations ([NotEmpty], [MaxLength], [Range]) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Annotation_InvalidName_IsInvalid(string? name)
    {
        var request = new CreateProductRequest(name!, null, 9.99m);

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
        var request = new CreateProductRequest(new string('A', 201), null, 9.99m);

        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Name"));
    }

    [Theory]
    [MemberData(
        nameof(PriceTheoryData.InvalidNegativeAmounts),
        MemberType = typeof(PriceTheoryData)
    )]
    public void Annotation_PriceNegative_IsInvalid(decimal price)
    {
        var request = new CreateProductRequest("Valid Name", null, price);

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
        var request = new CreateProductRequest("Valid Name", null, 0m);

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
        var request = new CreateProductRequest("Any name", description, price);
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
