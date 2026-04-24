using System.Text.RegularExpressions;

namespace Identity.Auth.Security.Sessions;

internal static partial class BffSessionIds
{
    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static bool IsValid(string sessionId) => Format().IsMatch(sessionId);
}
