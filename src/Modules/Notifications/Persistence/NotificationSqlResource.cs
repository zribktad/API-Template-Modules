using System.Reflection;

namespace Notifications.Persistence;

/// <summary>
///     Loads embedded PostgreSQL scripts from <c>StoredProcedures/Sql/</c> in this assembly.
/// </summary>
public static class NotificationSqlResource
{
    private const string SqlResourcePrefix = "Notifications.StoredProcedures.Sql.";

    private static readonly Assembly Assembly = typeof(NotificationSqlResource).Assembly;

    /// <param name="fileName">e.g. <c>claim_retryable_failed_emails_v2_up.sql</c></param>
    public static string Load(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid file name.", nameof(fileName));

        string suffix = fileName.Replace('/', '.').Replace('\\', '.');
        string resourceName = SqlResourcePrefix + suffix;

        using Stream? stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException(
                $"Embedded SQL resource '{resourceName}' not found. "
                    + $"Available: {string.Join(", ", Assembly.GetManifestResourceNames())}"
            );

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
