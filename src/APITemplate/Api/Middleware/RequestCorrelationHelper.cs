using BuildingBlocks.Application.Http;

namespace APITemplate.Api.Middleware;

/// <summary>
///     Shared correlation id resolution for request pipeline middleware.
/// </summary>
internal static class RequestCorrelationHelper
{
    private const int MaxCorrelationIdLength = 128;

    public static string ResolveCorrelationId(HttpContext context)
    {
        string incoming = context
            .Request.Headers[RequestContextConstants.Headers.CorrelationId]
            .ToString();

        if (string.IsNullOrWhiteSpace(incoming))
            return context.TraceIdentifier;

        // Sanitize the client-supplied value before it reaches logs: cap length and keep only safe
        // characters so it can't be used for log forgery / injection (e.g. embedded newlines).
        string sanitized = SanitizeCorrelationId(incoming);
        return sanitized.Length == 0 ? context.TraceIdentifier : sanitized;
    }

    private static string SanitizeCorrelationId(string value)
    {
        int length = Math.Min(value.Length, MaxCorrelationIdLength);
        Span<char> buffer = stackalloc char[length];
        int written = 0;
        for (int i = 0; i < length; i++)
        {
            char c = value[i];
            // Allow ASCII letters, digits, and a small set of separators commonly used in trace ids.
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':')
                buffer[written++] = c;
        }

        return new string(buffer[..written]);
    }
}
