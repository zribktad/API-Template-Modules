using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Notifications.Persistence;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

[Trait("Category", "Unit")]
public sealed class FailedEmailConcurrencyConfigurationTests
{
    [Fact]
    public void FailedEmailConfiguration_ConfiguresPostgresXminConcurrencyToken()
    {
        var builder = new ModelBuilder();
        builder.ApplyConfiguration(new FailedEmailConfiguration());

        var entityType = builder.Model.FindEntityType(typeof(FailedEmail));
        entityType.ShouldNotBeNull();
        var xmin = entityType!.FindProperty("xmin");
        xmin.ShouldNotBeNull();
        xmin.IsConcurrencyToken.ShouldBeTrue();
        xmin.GetColumnType().ShouldBe("xid");
    }
}
