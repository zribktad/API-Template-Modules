namespace APITemplate.Api.ExceptionHandling;

/// <summary>
///     Builds absolute <c>ProblemDetails.Type</c> URIs from <see cref="ErrorDocumentationOptions" />.
/// </summary>
internal static class ProblemDetailsErrorTypeUri
{
    public static bool IsValidBaseUriWhenSet(string? baseUri)
    {
        if (string.IsNullOrWhiteSpace(baseUri))
            return true;

        return Uri.TryCreate(baseUri.TrimEnd('/'), UriKind.Absolute, out Uri? u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
    }

    public static string? BuildAbsoluteUri(string? errorTypeBaseUri, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorTypeBaseUri) || string.IsNullOrWhiteSpace(errorCode))
            return null;

        string trimmedBase = errorTypeBaseUri.TrimEnd('/');
        string encodedCode = Uri.EscapeDataString(errorCode);
        string combined = $"{trimmedBase}/{encodedCode}";

        return Uri.TryCreate(combined, UriKind.Absolute, out Uri? absolute)
            ? absolute.AbsoluteUri
            : null;
    }
}
