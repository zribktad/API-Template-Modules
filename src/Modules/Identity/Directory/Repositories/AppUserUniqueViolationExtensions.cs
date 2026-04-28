using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Directory.Repositories;

/// <summary>
///     Recognizes PostgreSQL unique violations on the user's normalized email/username columns.
/// </summary>
public static class AppUserUniqueViolationExtensions
{
    private const string NormalizedEmail = "NormalizedEmail";
    private const string NormalizedUsername = "NormalizedUsername";

    public static bool IsUserEmailUniqueViolation(this DbUpdateException exception)
    {
        return IsUniqueViolationOnColumn(exception, NormalizedEmail);
    }

    public static bool IsUserUsernameUniqueViolation(this DbUpdateException exception)
    {
        return IsUniqueViolationOnColumn(exception, NormalizedUsername);
    }

    private static bool IsUniqueViolationOnColumn(DbUpdateException exception, string columnMarker)
    {
        if (exception.Message.Contains(columnMarker, StringComparison.OrdinalIgnoreCase))
            return true;

        if (exception.InnerException is not PostgresException postgresException)
            return false;

        if (postgresException.SqlState != PostgresErrorCodes.UniqueViolation)
            return false;

        return (
                postgresException.Detail?.Contains(columnMarker, StringComparison.OrdinalIgnoreCase)
                ?? false
            )
            || (
                postgresException.ConstraintName?.Contains(
                    columnMarker,
                    StringComparison.OrdinalIgnoreCase
                ) ?? false
            );
    }
}
