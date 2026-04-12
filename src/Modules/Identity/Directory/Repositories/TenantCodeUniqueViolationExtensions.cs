using Identity.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Directory.Repositories;

/// <summary>
///     Recognizes PostgreSQL unique violations on <see cref="TenantConfiguration.TenantCodeIndexName" />.
/// </summary>
public static class TenantCodeUniqueViolationExtensions
{
    public static bool IsTenantCodeUniqueViolation(this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                TenantConfiguration.TenantCodeIndexName,
                StringComparison.Ordinal
            );
    }
}
