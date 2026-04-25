using System.Text.RegularExpressions;

namespace Identity.Auth.Security.Sessions;

internal static partial class BffSessionIds
{
    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static bool IsValid(string sessionId) => Format().IsMatch(sessionId);

    // Log a short prefix only; full session ids are cookie-equivalent credentials and must not land
    // in structured logs un-redacted.
    public static string SafeRef(string sessionId) =>
        string.IsNullOrEmpty(sessionId)
            ? "(empty)"
            : sessionId[..Math.Min(8, sessionId.Length)] + "...";
}
