using System.Reflection;

namespace Notifications.Persistence;

/// <summary>
///     Loads embedded PostgreSQL scripts from <c>StoredProcedures/Sql/</c> in this assembly.
/// </summary>
public static class NotificationSqlResource
{
    private static readonly Assembly Assembly = typeof(NotificationSqlResource).Assembly;

    /// <param name="fileName">e.g. <c>claim_retryable_failed_emails_v2_up.sql</c></param>
    public static string Load(string fileName)
    {
        string? resourceName = Assembly
            .GetManifestResourceNames()
            .SingleOrDefault(n =>
                n.EndsWith(
                    "." + fileName.Replace('/', '.').Replace('\\', '.'),
                    StringComparison.Ordinal
                ) || n.EndsWith(fileName, StringComparison.Ordinal)
            );

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"Embedded SQL resource ending with '{fileName}' not found. "
                    + $"Available: {string.Join(", ", Assembly.GetManifestResourceNames())}"
            );
        }

        using Stream? stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException(
                $"Could not open manifest resource stream '{resourceName}'."
            );

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
