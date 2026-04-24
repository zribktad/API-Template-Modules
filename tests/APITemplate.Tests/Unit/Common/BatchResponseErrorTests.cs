using ErrorOr;
using SharedKernel.Application.DTOs;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Common;

/// <summary>
///     Covers the defensive contract of <see cref="BatchResponseError" />: <see cref="BatchResponseError.Unwrap" />
///     and <see cref="BatchResponseError.TryUnwrap" /> must only succeed for errors produced by
///     <see cref="BatchResponseError.From" /> and fail predictably for anything else.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BatchResponseErrorTests
{
    [Fact]
    public void From_ThenUnwrap_RoundtripsTheBatchResponse()
    {
        BatchResponse original = new(
            [new BatchResultItem(0, null, ["bad"])],
            SuccessCount: 0,
            FailureCount: 1
        );

        Error error = BatchResponseError.From(original);
        BatchResponse unwrapped = BatchResponseError.Unwrap(error);

        unwrapped.ShouldBeSameAs(original);
    }

    [Fact]
    public void TryUnwrap_ForErrorFromFrom_ReturnsTrueAndResponse()
    {
        BatchResponse original = new([], 1, 0);
        Error error = BatchResponseError.From(original);

        bool ok = BatchResponseError.TryUnwrap(error, out BatchResponse? response);

        ok.ShouldBeTrue();
        response.ShouldBeSameAs(original);
    }

    [Fact]
    public void TryUnwrap_ForUnrelatedError_ReturnsFalse()
    {
        Error unrelated = Error.Validation("Other.Code", "unrelated");

        bool ok = BatchResponseError.TryUnwrap(unrelated, out BatchResponse? response);

        ok.ShouldBeFalse();
        response.ShouldBeNull();
    }

    [Fact]
    public void TryUnwrap_ForMatchingCodeButMissingMetadata_ReturnsFalse()
    {
        // Someone constructs an error with our code but forgets to attach metadata; Unwrap must
        // not silently succeed with a bogus value.
        Error spoofed = Error.Validation(BatchResponseError.Code, "spoofed");

        bool ok = BatchResponseError.TryUnwrap(spoofed, out BatchResponse? response);

        ok.ShouldBeFalse();
        response.ShouldBeNull();
    }

    [Fact]
    public void Unwrap_ForUnrelatedError_ThrowsInvalidOperationException()
    {
        Error unrelated = Error.NotFound("Thing.NotFound", "missing");

        Should
            .Throw<InvalidOperationException>(() => BatchResponseError.Unwrap(unrelated))
            .Message.ShouldContain("Thing.NotFound");
    }
}
