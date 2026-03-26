using System.Reflection;

namespace APITemplate.Infrastructure.Database;

/// <summary>
/// Loads embedded SQL files from <c>Infrastructure/Database/**</c>.
/// SQL files are compiled into the assembly as embedded resources,
/// so they work correctly after publish without relying on the file system.
/// </summary>
public static class SqlResource
{
    private const string Namespace = "APITemplate.Infrastructure.Database";

    public static string Load(string relativeResourcePath)
    {
        var normalizedPath = relativeResourcePath
            .Replace('\\', '.')
            .Replace('/', '.');

        var resourceName = $"{Namespace}.{normalizedPath}";
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded SQL resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
