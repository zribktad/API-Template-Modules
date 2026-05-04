using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Options;

/// <summary>
///     Binds to "Request" section. Centralizes limits and flags for request handling.
/// </summary>
public sealed class RequestOptions
{
    public const string Section = "Request";

    /// <summary>
    ///     Maximum request body size in Megabytes.
    ///     Enforced at Kestrel and IIS levels.
    /// </summary>
    [Range(1, 1024)]
    public int RequestSizeLimitMb { get; init; } = 1;

    /// <summary>
    ///     Convenience property to get the limit in bytes.
    /// </summary>
    public long RequestSizeLimitBytes => (long)RequestSizeLimitMb * 1024 * 1024;
}
