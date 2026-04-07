using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace SharedKernel.Infrastructure.Persistence;

/// <summary>
///     Development-time relational schema helpers shared by module database startup contributors.
/// </summary>
public static class RelationalDatabaseSchemaExtensions
{
    /// <summary>PostgreSQL: duplicate_table / relation already exists.</summary>
    private const string PgDuplicateRelationSqlState = "42P07";

    /// <summary>
    ///     Ensures the database exists and creates tables when missing (PostgreSQL), ignoring
    ///     duplicate-relation errors from concurrent startup.
    /// </summary>
    public static async Task EnsureCreatedAndTablesAsync(
        this DbContext context,
        CancellationToken cancellationToken = default
    )
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        IRelationalDatabaseCreator creator = context.GetService<IRelationalDatabaseCreator>();

        // Skip CreateTables when all tables already exist — avoids noisy ERR log from EF Core
        // when Postgres rejects the duplicate DDL before the catch block can handle it.
        // The try/catch below still guards against the rare concurrent-startup race.
        if (await creator.HasTablesAsync(cancellationToken))
            return;

        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PgDuplicateRelationSqlState)
        {
            // Concurrent startup may race CreateTables — safe to ignore.
        }
    }
}
