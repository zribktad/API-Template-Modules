using System.Diagnostics.CodeAnalysis;
using ErrorOr;

namespace BuildingBlocks.Application.DTOs;

/// <summary>
///     Bridges the <see cref="BatchResponse" /> failure shape into <see cref="ErrorOr{TValue}" /> returns.
///     The full batch response is carried via metadata so callers can forward it as the response payload
///     without losing per-item failure detail.
/// </summary>
public static class BatchResponseError
{
    public const string Code = "Batch.ValidationFailed";
    private const string MetadataKey = "batchResponse";

    /// <summary>Wraps the given <paramref name="response" /> as a single <see cref="Error" />.</summary>
    public static Error From(BatchResponse response)
    {
        return Error.Validation(
            Code,
            "One or more items failed batch validation.",
            new Dictionary<string, object> { [MetadataKey] = response }
        );
    }

    /// <summary>
    ///     Extracts the originating <see cref="BatchResponse" /> from an error produced by <see cref="From" />.
    ///     Throws <see cref="InvalidOperationException" /> when called with any other <see cref="Error" />.
    /// </summary>
    public static BatchResponse Unwrap(Error error)
    {
        if (!TryUnwrap(error, out BatchResponse? response))
        {
            throw new InvalidOperationException(
                $"Error with code '{error.Code}' was not produced by {nameof(BatchResponseError)}.{nameof(From)}."
            );
        }

        return response;
    }

    /// <summary>
    ///     Attempts to extract the originating <see cref="BatchResponse" /> from <paramref name="error" />.
    ///     Returns <c>false</c> when the error was not produced by <see cref="From" />.
    /// </summary>
    public static bool TryUnwrap(Error error, [NotNullWhen(true)] out BatchResponse? response)
    {
        if (
            error.Code == Code
            && error.Metadata is { } metadata
            && metadata.TryGetValue(MetadataKey, out object? value)
            && value is BatchResponse wrapped
        )
        {
            response = wrapped;
            return true;
        }

        response = null;
        return false;
    }
}
