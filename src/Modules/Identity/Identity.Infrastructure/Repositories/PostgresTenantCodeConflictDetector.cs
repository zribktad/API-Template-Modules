using Identity.Application.Features.Tenant;
using Identity.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Infrastructure.Repositories;

public sealed class PostgresTenantCodeConflictDetector : ITenantCodeConflictDetector
{
    public bool IsCodeConflict(Exception exception) =>
        exception is DbUpdateException dbUpdateException
        && dbUpdateException.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(
            postgresException.ConstraintName,
            TenantConfiguration.TenantCodeIndexName,
            StringComparison.Ordinal
        );
}
