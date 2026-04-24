using System.ComponentModel.DataAnnotations;
using APITemplate.Tests.Unit.Helpers;
using Chatting.Features.GetNotificationStream;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Chatting;

[Trait("Category", "Unit")]
public sealed class SseStreamRequestValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validation_WhenCountOutOfRange_Fails(int count)
    {
        SseStreamRequest request = new() { Count = count };
        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(
            request,
            out List<ValidationResult> results
        );

        valid.ShouldBeFalse();
        results.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validation_WhenCountInRange_Passes(int count)
    {
        SseStreamRequest request = new() { Count = count };
        bool valid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out _);

        valid.ShouldBeTrue();
    }
}
