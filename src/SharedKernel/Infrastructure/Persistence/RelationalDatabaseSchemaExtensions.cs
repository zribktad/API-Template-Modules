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

        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
            // 42P07 = relation already exists — safe to ignore
        }
    }
}
