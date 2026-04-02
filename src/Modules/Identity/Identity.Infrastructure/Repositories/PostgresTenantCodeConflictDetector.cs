using Npgsql;

namespace Identity.Infrastructure.Repositories;

public sealed class PostgresTenantCodeConflictDetector : ITenantCodeConflictDetector
{
    private const string TenantCodeIndexName = "IX_Tenants_Code";

    public bool IsCodeConflict(Exception exception) =>
        exception is DbUpdateException dbUpdateException
        && dbUpdateException.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(
            postgresException.ConstraintName,
            TenantCodeIndexName,
            StringComparison.Ordinal
        );
}
