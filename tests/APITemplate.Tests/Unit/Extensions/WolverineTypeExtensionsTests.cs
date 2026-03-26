using APITemplate.Api.Extensions;
using ErrorOr;
using FluentValidation;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Extensions;

public sealed record WrappedPayload(string Name);

public sealed record WrappedMessage(WrappedPayload Payload);

public sealed record PlainMessage(Guid Id);

public class WolverineTypeExtensionsTests
{
    private sealed class WrappedPayloadValidator : AbstractValidator<WrappedPayload>
    {
        public WrappedPayloadValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    [Fact]
    public void HasValidatorIn_WhenValidatorTargetsWrappedPayload_ReturnsFalse()
    {
        typeof(WrappedMessage)
            .HasValidatorIn(typeof(WrappedPayloadValidator).Assembly)
            .ShouldBeFalse();
    }

    [Fact]
    public void HasValidatorIn_WhenNoMessageOrPayloadValidatorExists_ReturnsFalse()
    {
        typeof(PlainMessage)
            .HasValidatorIn(typeof(WrappedPayloadValidator).Assembly)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsErrorOrReturnType_WhenTaskWrapsErrorOr_ReturnsTrue()
    {
        typeof(Task<ErrorOr<string>>).IsErrorOrReturnType().ShouldBeTrue();
    }
}
