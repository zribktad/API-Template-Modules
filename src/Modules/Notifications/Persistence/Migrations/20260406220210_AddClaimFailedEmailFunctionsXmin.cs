using Microsoft.EntityFrameworkCore.Migrations;
using Notifications.Persistence;

#nullable disable

namespace Notifications.Persistence.Migrations;

/// <inheritdoc />
/// <remarks>
///     Installs PostgreSQL claim functions returning <c>xmin</c> for EF optimistic concurrency.
///     Depends on <see cref="InitialNotifications" /> ( <c>FailedEmails</c> table ).
/// </remarks>
public partial class AddClaimFailedEmailFunctionsXmin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            NotificationSqlResource.Load("claim_retryable_failed_emails_v2_up.sql")
        );
        migrationBuilder.Sql(NotificationSqlResource.Load("claim_expired_failed_emails_v2_up.sql"));
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <c>v1_down</c> drops by argument list; same signature as v2, so the current function body is removed.
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            NotificationSqlResource.Load("claim_retryable_failed_emails_v1_down.sql")
        );
        migrationBuilder.Sql(
            NotificationSqlResource.Load("claim_expired_failed_emails_v1_down.sql")
        );
    }
}
