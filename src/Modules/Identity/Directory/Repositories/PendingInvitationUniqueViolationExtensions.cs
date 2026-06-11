using Identity.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Directory.Repositories;

/// <summary>
///     Recognizes PostgreSQL unique violations on
///     <see cref="TenantInvitationConfiguration.PendingInvitationIndexName" /> (the partial
///     one-pending-invitation-per-(tenant, email) index).
/// </summary>
public static class PendingInvitationUniqueViolationExtensions
{
    public static bool IsPendingInvitationUniqueViolation(this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                TenantInvitationConfiguration.PendingInvitationIndexName,
                StringComparison.Ordinal
            );
    }
}
