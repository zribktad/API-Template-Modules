using Npgsql;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Creates a dedicated database on the shared Testcontainers Postgres instance for isolated integration tests.
/// </summary>
internal static class IsolatedPostgresDatabase
{
    public static async Task CreateDatabaseAsync(
        string serverConnectionString,
        string databaseName,
        CancellationToken ct = default
    )
    {
        await using NpgsqlConnection conn = new(serverConnectionString);
        await conn.OpenAsync(ct);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static string BuildConnectionString(
        string serverConnectionString,
        string databaseName
    ) =>
        new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;

    public static async Task<string> CreateAndGetConnectionStringAsync(
        SharedPostgresContainer postgres,
        string databaseName,
        CancellationToken ct = default
    )
    {
        await CreateDatabaseAsync(postgres.ServerConnectionString, databaseName, ct);
        return BuildConnectionString(postgres.ServerConnectionString, databaseName);
    }
}
