using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options.Infrastructure;

/// <summary>
/// Configuration for the Dragonfly (Redis-compatible) connection used for distributed caching
/// and background-job coordination.
/// </summary>
public sealed class DragonflyOptions
{
    public const int DefaultConnectTimeoutMs = 5000;
    public const int DefaultSyncTimeoutMs = 3000;

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ConnectTimeoutMs { get; init; } = DefaultConnectTimeoutMs;

    [Range(1, int.MaxValue)]
    public int SyncTimeoutMs { get; init; } = DefaultSyncTimeoutMs;
}
