using APITemplate.Api.Extensions.Startup;
using Identity.Auth.Features.Bff.DTOs;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using SharedKernel.Infrastructure.Logging;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Logging;

[Trait("Category", "Unit")]
public partial class DataRedactionTests
{
    [Fact]
    public void PersonalDataAttribute_ShouldMapToPersonalClassification()
    {
        // Arrange & Act
        var attribute = new PersonalDataAttribute();

        // Assert
        attribute.Classification.ShouldBe(LogDataClassifications.Personal);
    }

    [Fact]
    public void SensitiveDataAttribute_ShouldMapToSensitiveClassification()
    {
        // Arrange & Act
        var attribute = new SensitiveDataAttribute();

        // Assert
        attribute.Classification.ShouldBe(LogDataClassifications.Sensitive);
    }

    [Theory]
    [InlineData(typeof(AppUser), nameof(AppUser.Email))]
    [InlineData(typeof(BffUserResponse), nameof(BffUserResponse.Email))]
    [InlineData(typeof(UserResponse), nameof(UserResponse.Email))]
    [InlineData(typeof(CreateUserRequest), nameof(CreateUserRequest.Email))]
    [InlineData(typeof(UpdateUserRequest), nameof(UpdateUserRequest.Email))]
    [InlineData(typeof(EmailMessage), nameof(EmailMessage.To))]
    public void Property_ShouldHavePersonalDataAttribute(Type type, string propertyName)
    {
        // Arrange
        var property = type.GetProperty(propertyName);

        // Act
        var attribute = property
            ?.GetCustomAttributes(typeof(PersonalDataAttribute), false)
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull(
            $"Property {type.Name}.{propertyName} should have [PersonalData] attribute."
        );
    }

    [Theory]
    [InlineData(typeof(EmailOptions), nameof(EmailOptions.Username))]
    [InlineData(typeof(EmailOptions), nameof(EmailOptions.Password))]
    public void Property_ShouldHaveSensitiveDataAttribute(Type type, string propertyName)
    {
        // Arrange
        var property = type.GetProperty(propertyName);

        // Act
        var attribute = property
            ?.GetCustomAttributes(typeof(SensitiveDataAttribute), false)
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull(
            $"Property {type.Name}.{propertyName} should have [SensitiveData] attribute."
        );
    }

    [Fact]
    public void ResolveAppUserAccessHandler_DenyAndLog_ParametersShouldHavePersonalDataAttribute()
    {
        // Arrange
        var method = typeof(ResolveAppUserAccessHandler).GetMethod(
            "DenyAndLog",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

        // Act
        var parameter = method?.GetParameters().FirstOrDefault(p => p.Name == "normalizedEmail");
        var attribute = parameter
            ?.GetCustomAttributes(typeof(PersonalDataAttribute), false)
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull(
            "Parameter normalizedEmail in DenyAndLog should have [PersonalData] attribute."
        );
    }

    [Fact]
    public void Logger_ShouldRedactDataWithHmac()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Redaction:HmacKey"] = "dGhpcyBpcyBhIHZlcnkgc2VjcmV0IGhtYWMga2V5IDEyMzQ1Njc4", // 32 bytes base64
                    ["Redaction:KeyId"] = "1",
                }
            )
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging =>
        {
            logging.EnableRedaction();
        });

        // Use the actual production setup via the extension method
        services.AddApplicationRedaction(configuration);

        var provider = new TestLoggerProvider();
        services.AddSingleton<ILoggerProvider>(provider);

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataRedactionTests>>();

        var tenantId = Guid.NewGuid();
        var email = "test@example.com";
        var secretId = "secret-id-123";

        // Act
        LogTenantSensitiveData(logger, tenantId, email, secretId);

        // Assert
        provider.Logs.Count.ShouldBe(1);
        var log = provider.Logs[0];

        // TenantId should be in plain text
        log.ShouldContain(tenantId.ToString());

        // Email should be HMACed (Format is KeyId:HMAC)
        log.ShouldNotContain(email);
        log.ShouldMatch($@"Tenant {tenantId}: User 1:[a-zA-Z0-9+/=]+ has secret 1:[a-zA-Z0-9+/=]+");
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Tenant {TenantId}: User {Email} has secret {Id}"
    )]
    private static partial void LogTenantSensitiveData(
        ILogger logger,
        Guid tenantId,
        [PersonalData] string email,
        [SensitiveData] string id
    );

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        public List<string> Logs { get; } = new();

        public ILogger CreateLogger(string categoryName) => new TestLogger(this);

        public void Dispose() { }

        private sealed class TestLogger : ILogger
        {
            private readonly TestLoggerProvider _provider;

            public TestLogger(TestLoggerProvider provider) => _provider = provider;

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                _provider.Logs.Add(formatter(state, exception));
            }
        }
    }
}
