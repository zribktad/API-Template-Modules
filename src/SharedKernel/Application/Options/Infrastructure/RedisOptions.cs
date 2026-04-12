using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.Infrastructure;

/// <summary>
///     Configuration for the Redis connection (via StackExchange.Redis) used for distributed caching
///     and background-job coordination.
/// </summary>
public sealed class RedisOptions
{
    public const int DefaultConnectTimeoutMs = 5000;
    public const int DefaultSyncTimeoutMs = 3000;

    [Description("Redis connection string (StackExchange.Redis format).")]
    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Description("Connection timeout, in milliseconds, for establishing a Redis connection.")]
    [Range(1, int.MaxValue)]
    public int ConnectTimeoutMs { get; init; } = DefaultConnectTimeoutMs;

    [Description("Synchronous operation timeout, in milliseconds, for Redis commands.")]
    [Range(1, int.MaxValue)]
    public int SyncTimeoutMs { get; init; } = DefaultSyncTimeoutMs;
}
