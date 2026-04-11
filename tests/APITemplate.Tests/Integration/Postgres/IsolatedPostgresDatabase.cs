using Npgsql;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Creates a dedicated database on the shared Testcontainers Postgres instance for isolated integration tests.
/// </summary>
internal static class IsolatedPostgresDatabase
{
    public static async Task<string> CreateAndGetConnectionStringAsync(
        SharedPostgresContainer postgres,
        string databaseName,
        CancellationToken ct = default
    )
    {
        await using NpgsqlConnection conn = new(postgres.ServerConnectionString);
        await conn.OpenAsync(ct);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync(ct);

        return new NpgsqlConnectionStringBuilder(postgres.ServerConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }
}
