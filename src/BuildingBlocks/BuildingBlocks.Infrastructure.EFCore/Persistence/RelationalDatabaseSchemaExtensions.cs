using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BuildingBlocks.Infrastructure.EFCore.Persistence;

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

        // Always attempt CreateTables — each DbContext creates only its own tables.
        // HasTablesAsync() is DB-wide and would incorrectly skip this context's tables
        // when another module has already created theirs. The catch handles duplicates.
        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PgDuplicateRelationSqlState)
        {
            // Tables already exist (another module ran first, or concurrent startup) — safe to ignore.
        }
    }
}

