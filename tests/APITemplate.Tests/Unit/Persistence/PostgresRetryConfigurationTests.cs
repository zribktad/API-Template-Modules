using System.Data;
using APITemplate.Api.Extensions;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public class PostgresRetryConfigurationTests
{
    [Fact]
    public void AddPersistence_BindsConfiguredTransactionDefaults()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                    ["TransactionDefaults:IsolationLevel"] = "Serializable",
                    ["TransactionDefaults:TimeoutSeconds"] = "45",
                    ["TransactionDefaults:RetryEnabled"] = "true",
                    ["TransactionDefaults:RetryCount"] = "7",
                    ["TransactionDefaults:RetryDelaySeconds"] = "11",
                }
            )
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TransactionDefaultsOptions>>().Value;

        options.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
        options.TimeoutSeconds.ShouldBe(45);
        options.RetryEnabled.ShouldBeTrue();
        options.RetryCount.ShouldBe(7);
        options.RetryDelaySeconds.ShouldBe(11);
    }

    [Fact]
    public void AddPersistence_UsesDefaultTransactionSettingsWhenSectionMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                }
            )
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TransactionDefaultsOptions>>().Value;

        options.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        options.TimeoutSeconds.ShouldBe(30);
        options.RetryEnabled.ShouldBeTrue();
        options.RetryCount.ShouldBe(3);
        options.RetryDelaySeconds.ShouldBe(5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigurePostgresDbContext_DoesNotEnableProviderLevelRetries(bool retryEnabled)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                    ["TransactionDefaults:RetryEnabled"] = retryEnabled.ToString(),
                }
            )
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1, 3, 5, "TimeoutSeconds")]
    [InlineData(30, -1, 5, "RetryCount")]
    [InlineData(30, 3, -1, "RetryDelaySeconds")]
    public void Resolve_WhenConfiguredDefaultsContainNegativeValues_Throws(
        int timeoutSeconds,
        int retryCount,
        int retryDelaySeconds,
        string expectedParameter
    )
    {
        var options = new TransactionDefaultsOptions
        {
            TimeoutSeconds = timeoutSeconds,
            RetryCount = retryCount,
            RetryDelaySeconds = retryDelaySeconds,
        };

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => options.Resolve(null));

        ex.ParamName.ShouldBe(expectedParameter);
    }

    [Theory]
    [InlineData(-1, null, null, "TimeoutSeconds")]
    [InlineData(null, -1, null, "RetryCount")]
    [InlineData(null, null, -1, "RetryDelaySeconds")]
    public void Resolve_WhenOverridesContainNegativeValues_Throws(
        int? timeoutSeconds,
        int? retryCount,
        int? retryDelaySeconds,
        string expectedParameter
    )
    {
        var defaults = new TransactionDefaultsOptions();
        var overrides = new Domain.Options.TransactionOptions
        {
            TimeoutSeconds = timeoutSeconds,
            RetryCount = retryCount,
            RetryDelaySeconds = retryDelaySeconds,
        };

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => defaults.Resolve(overrides));

        ex.ParamName.ShouldBe(expectedParameter);
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
