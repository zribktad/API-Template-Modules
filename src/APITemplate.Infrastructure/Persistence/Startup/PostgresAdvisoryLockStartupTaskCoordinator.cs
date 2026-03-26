using APITemplate.Application.Common.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace APITemplate.Infrastructure.Persistence.Startup;

/// <summary>
/// Serializes selected startup tasks across multiple application instances by taking a
/// PostgreSQL advisory lock for the duration of the task.
/// </summary>
/// <remarks>
/// This coordinator exists because startup bootstrap work is not safe to run concurrently
/// when several nodes start against the same database at the same time. Without coordination,
/// parallel execution of relational migrations, auth/bootstrap seeding, or background-job
/// scheduler bootstrap can race and fail with duplicate work, conflicting DDL, or partially
/// initialized shared state.
///
/// PostgreSQL advisory locks provide a process-independent mutex keyed by an application-defined
/// number. Each startup task name is mapped to a stable lock key so only one instance executes
/// that task at a time, while other instances wait. The lock is held on a dedicated Npgsql
/// connection because advisory locks are scoped to the database session that acquired them.
///
/// When the active instance finishes, or its connection is dropped, PostgreSQL releases the
/// lock and another instance may continue. For non-PostgreSQL providers this type returns a
/// no-op async disposable lease, because advisory locks are a PostgreSQL-specific
/// coordination mechanism.
/// </remarks>
public sealed class PostgresAdvisoryLockStartupTaskCoordinator : IStartupTaskCoordinator
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PostgresAdvisoryLockStartupTaskCoordinator> _logger;

    public PostgresAdvisoryLockStartupTaskCoordinator(
        AppDbContext dbContext,
        ILogger<PostgresAdvisoryLockStartupTaskCoordinator> logger
    )
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Acquires a startup-task lease backed by a PostgreSQL advisory lock when the current
    /// relational provider is Npgsql.
    /// </summary>
    /// <param name="startupTask">
    /// Logical startup task identifier. Enum values are stable and double as advisory lock keys.
    /// </param>
    /// <param name="ct">Cancellation token used while waiting for the advisory lock.</param>
    public async Task<IAsyncDisposable> AcquireAsync(
        StartupTaskName startupTask,
        CancellationToken ct = default
    )
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            return NoOpAsyncDisposable.Instance;
        }

        var connectionString =
            _dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "PostgreSQL startup coordination requires a relational connection string."
            );

        var advisoryConnection = new NpgsqlConnection(connectionString);

        try
        {
            await advisoryConnection.OpenAsync(ct);

            await using var lockCommand = advisoryConnection.CreateCommand();
            lockCommand.CommandText = "SELECT pg_advisory_lock(@lockKey)";
            lockCommand.Parameters.AddWithValue("lockKey", (long)startupTask);

            _logger.LogDebug("Waiting for startup coordination lock {StartupTask}.", startupTask);
            await lockCommand.ExecuteNonQueryAsync(ct);

            return new PostgresAdvisoryLockLease(advisoryConnection, startupTask);
        }
        catch
        {
            await advisoryConnection.DisposeAsync();
            throw;
        }
    }

    private sealed class PostgresAdvisoryLockLease(
        NpgsqlConnection advisoryConnection,
        StartupTaskName startupTask
    ) : IAsyncDisposable
    {
        private bool _disposed;

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await using var unlockCommand = advisoryConnection.CreateCommand();
            unlockCommand.CommandText = "SELECT pg_advisory_unlock(@lockKey)";
            unlockCommand.Parameters.AddWithValue("lockKey", (long)startupTask);
            await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
            await advisoryConnection.DisposeAsync();
        }
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public static NoOpAsyncDisposable Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
