using APITemplate.Application.Common.Middleware;
using ErrorOr;
using FluentValidation;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Middleware;

public sealed record MiddlewareTestCommand(string Name, decimal Price);

public class ErrorOrValidationMiddlewareTests
{
    private sealed class HappyPathValidator : AbstractValidator<MiddlewareTestCommand>
    {
        public HappyPathValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private sealed class NameRequiredValidator : AbstractValidator<MiddlewareTestCommand>
    {
        public NameRequiredValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    private sealed class MultiFieldValidator : AbstractValidator<MiddlewareTestCommand>
    {
        public MultiFieldValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
        }
    }

    [Fact]
    public async Task BeforeAsync_WhenNoValidator_ReturnsContinue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (continuation, result) = await ErrorOrValidationMiddleware.BeforeAsync<
            MiddlewareTestCommand,
            string
        >(new MiddlewareTestCommand("Widget", 10m), validator: null, ct: ct);

        continuation.ShouldBe(HandlerContinuation.Continue);
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task BeforeAsync_WhenValidationPasses_ReturnsContinue()
    {
        var ct = TestContext.Current.CancellationToken;
        var validator = new HappyPathValidator();

        var (continuation, result) = await ErrorOrValidationMiddleware.BeforeAsync<
            MiddlewareTestCommand,
            string
        >(new MiddlewareTestCommand("Widget", 10m), validator, ct);

        continuation.ShouldBe(HandlerContinuation.Continue);
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task BeforeAsync_WhenValidationFails_ReturnsStopWithErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var validator = new NameRequiredValidator();

        var (continuation, result) = await ErrorOrValidationMiddleware.BeforeAsync<
            MiddlewareTestCommand,
            string
        >(new MiddlewareTestCommand("", 10m), validator, ct);

        continuation.ShouldBe(HandlerContinuation.Stop);
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("GEN-0400");
        result.FirstError.Description.ShouldBe("Name is required.");
        result.FirstError.Metadata.ShouldNotBeNull();
        result.FirstError.Metadata.ShouldContainKey("propertyName");
        result.FirstError.Metadata["propertyName"].ShouldBe("Name");
    }

    [Fact]
    public async Task BeforeAsync_WhenMultipleValidationFailures_ReturnsAllErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var validator = new MultiFieldValidator();

        var (continuation, result) = await ErrorOrValidationMiddleware.BeforeAsync<
            MiddlewareTestCommand,
            string
        >(new MiddlewareTestCommand("", 0m), validator, ct);

        continuation.ShouldBe(HandlerContinuation.Stop);
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Errors.All(e => e.Type == ErrorType.Validation).ShouldBeTrue();
        result.Errors.All(e => e.Code == "GEN-0400").ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description == "Name is required.");
        result.Errors.ShouldContain(e => e.Description == "Price must be greater than zero.");
        result.Errors.Any(e => HasPropertyName(e, "Name")).ShouldBeTrue();
        result.Errors.Any(e => HasPropertyName(e, "Price")).ShouldBeTrue();
    }

    private static bool HasPropertyName(Error error, string expectedPropertyName) =>
        error.Metadata is not null
        && error.Metadata.TryGetValue("propertyName", out var propertyName)
        && propertyName as string == expectedPropertyName;
}
