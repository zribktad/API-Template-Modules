namespace BuildingBlocks.Web.Api;

/// <summary>
///     Builds absolute <c>ProblemDetails.Type</c> URIs from <see cref="ErrorDocumentationOptions" />.
/// </summary>
public static class ProblemDetailsErrorTypeUri
{
    public static bool IsValidBaseUriWhenSet(string? baseUri)
    {
        if (!TryGetTrimmedBase(baseUri, out string trimmed))
            return true;

        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
    }

    public static string? BuildAbsoluteUri(string? errorTypeBaseUri, string errorCode)
    {
        if (
            !TryGetTrimmedBase(errorTypeBaseUri, out string trimmedBase)
            || string.IsNullOrWhiteSpace(errorCode)
        )
            return null;

        string encodedCode = Uri.EscapeDataString(errorCode);
        string combined = $"{trimmedBase}/{encodedCode}";

        return Uri.TryCreate(combined, UriKind.Absolute, out Uri? absolute)
            ? absolute.AbsoluteUri
            : null;
    }

    public static string BuildFallbackUri(string scheme, string host, string errorCode)
    {
        string encodedCode = Uri.EscapeDataString(errorCode);
        return $"{scheme}://{host}/errors/{encodedCode}";
    }

    private static bool TryGetTrimmedBase(string? baseUri, out string trimmed)
    {
        trimmed = "";
        if (string.IsNullOrWhiteSpace(baseUri))
            return false;

        trimmed = baseUri.TrimEnd('/');
        return true;
    }
}

