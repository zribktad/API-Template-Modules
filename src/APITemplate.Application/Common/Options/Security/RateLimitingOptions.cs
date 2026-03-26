using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options.Security;

/// <summary>
/// Configuration for the sliding-window rate-limiting policy applied to inbound API requests.
/// </summary>
public sealed class RateLimitingOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int WindowMinutes { get; set; } = 1;
}
