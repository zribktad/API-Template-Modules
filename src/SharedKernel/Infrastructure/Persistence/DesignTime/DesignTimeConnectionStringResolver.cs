using Microsoft.Extensions.Configuration;

namespace SharedKernel.Infrastructure.Persistence.DesignTime;

/// <summary>
///     Resolves the connection string for EF Core design-time factories (<c>dotnet ef migrations</c>).
///     Walks up the directory tree to find <c>APITemplate/Api/appsettings.json</c>, loads environment-aware
///     configuration, and falls back to environment variables.
/// </summary>
public static class DesignTimeConnectionStringResolver
{
    public static string Resolve()
    {
        string configDir = ResolveApiConfigurationDirectory();

        string environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(configDir)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required for design-time operations. "
                    + "Set it in appsettings.json, appsettings.{Environment}.json, or via the "
                    + "ConnectionStrings__DefaultConnection environment variable."
            );
    }

    /// <summary>
    ///     Locates <c>APITemplate/Api/appsettings.json</c> by walking up from the process cwd and from the
    ///     calling assembly directory so <c>dotnet ef</c> works from the repo root, module folder, or <c>bin/</c> output.
    /// </summary>
    private static string ResolveApiConfigurationDirectory()
    {
        foreach (string root in GetSearchRoots())
        {
            DirectoryInfo? dir = new(root);
            while (dir is not null)
            {
                foreach (
                    string candidate in new[]
                    {
                        Path.Combine(dir.FullName, "src", "APITemplate", "Api"),
                        Path.Combine(dir.FullName, "APITemplate", "Api"),
                    }
                )
                {
                    if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                        return candidate;
                }

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not find APITemplate/Api/appsettings.json. "
                + "Run `dotnet ef` from the repository tree, or set ConnectionStrings__DefaultConnection."
        );
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();

        string? assemblyDir = Path.GetDirectoryName(
            typeof(DesignTimeConnectionStringResolver).Assembly.Location
        );
        if (!string.IsNullOrEmpty(assemblyDir))
            yield return assemblyDir;
    }
}
