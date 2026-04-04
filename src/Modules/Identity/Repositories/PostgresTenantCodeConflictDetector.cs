using Identity.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Repositories;

public sealed class PostgresTenantCodeConflictDetector : ITenantCodeConflictDetector
{
    public bool IsCodeConflict(Exception exception)
    {
        return exception is DbUpdateException dbUpdateException
            && dbUpdateException.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                TenantConfiguration.TenantCodeIndexName,
                StringComparison.Ordinal
            );
    }
}
