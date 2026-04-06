using Notifications.Persistence;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

public sealed class NotificationSqlResourceTests
{
    [Fact]
    public void Load_v2_up_scripts_contain_xmin_return()
    {
        string retryable = NotificationSqlResource.Load("claim_retryable_failed_emails_v2_up.sql");
        retryable.ShouldContain("\"xmin\"");
        retryable.ShouldContain("failed.xmin");

        string expired = NotificationSqlResource.Load("claim_expired_failed_emails_v2_up.sql");
        expired.ShouldContain("\"xmin\"");
        expired.ShouldContain("failed.xmin");
    }
}
