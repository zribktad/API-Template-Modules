using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.Infrastructure;

/// <summary>
///     Configuration for the Dragonfly (Redis-compatible) connection used for distributed caching
///     and background-job coordination.
/// </summary>
public sealed class DragonflyOptions
{
    public const int DefaultConnectTimeoutMs = 5000;
    public const int DefaultSyncTimeoutMs = 3000;

    [Description("Redis-compatible connection string used to reach Dragonfly.")]
    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Description("Connection timeout, in milliseconds, for establishing a Dragonfly connection.")]
    [Range(1, int.MaxValue)]
    public int ConnectTimeoutMs { get; init; } = DefaultConnectTimeoutMs;

    [Description("Synchronous operation timeout, in milliseconds, for Dragonfly commands.")]
    [Range(1, int.MaxValue)]
    public int SyncTimeoutMs { get; init; } = DefaultSyncTimeoutMs;
}
