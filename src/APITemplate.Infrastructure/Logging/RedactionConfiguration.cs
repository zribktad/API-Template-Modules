namespace APITemplate.Infrastructure.Logging;

/// <summary>
/// Provides helper methods for resolving redaction configuration values, centralising the
/// precedence logic (environment variable first, then options, then error) used at startup.
/// </summary>
public static class RedactionConfiguration
{
    /// <summary>
    /// Resolves the HMAC key for log redaction by checking the environment variable named in
    /// <paramref name="options"/> first, then falling back to the inline <c>HmacKey</c> value.
    /// Throws <see cref="InvalidOperationException"/> if neither source provides a non-empty key.
    /// </summary>
    public static string ResolveHmacKey(
        RedactionOptions options,
        Func<string, string?> getEnvironmentVariable
    )
    {
        var key = getEnvironmentVariable(options.HmacKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        if (!string.IsNullOrWhiteSpace(options.HmacKey))
            return options.HmacKey;

        throw new InvalidOperationException(
            $"Missing redaction HMAC key. Set environment variable '{options.HmacKeyEnvironmentVariable}' or configure 'Redaction:HmacKey'."
        );
    }
}
