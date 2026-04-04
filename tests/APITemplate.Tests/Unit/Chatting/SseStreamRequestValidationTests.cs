using System.ComponentModel.DataAnnotations;
using Chatting.Features.GetNotificationStream;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Chatting;

public sealed class SseStreamRequestValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validation_WhenCountOutOfRange_Fails(int count)
    {
        SseStreamRequest request = new() { Count = count };
        var results = new List<ValidationResult>();
        bool valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true
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
        var results = new List<ValidationResult>();
        bool valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true
        );

        valid.ShouldBeTrue();
    }
}
