using Microsoft.Extensions.Configuration;

namespace APITemplate.Infrastructure.Persistence;

internal static class DesignTimeConfigurationHelper
{
    private const string DefaultConnectionName = "DefaultConnection";
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=apitemplate;Username=postgres;Password=postgres";

    public static IConfigurationRoot BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    public static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(DefaultConnectionName) ?? FallbackConnectionString;
}
