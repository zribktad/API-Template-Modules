using ErrorOr;

namespace SharedKernel.Application.DTOs;

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
    /// </summary>
    public static BatchResponse Unwrap(Error error)
    {
        return (BatchResponse)error.Metadata![MetadataKey];
    }
}
