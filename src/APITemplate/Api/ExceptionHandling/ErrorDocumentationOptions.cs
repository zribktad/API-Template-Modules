namespace APITemplate.Api.ExceptionHandling;

/// <summary>
///     Configuration for RFC 7807 <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails.Type" /> URIs.
///     Bound from section <c>ErrorDocumentation</c> (type name minus <c>Options</c> suffix).
/// </summary>
public sealed class ErrorDocumentationOptions
{
    /// <summary>
    ///     Absolute base URI for problem <c>type</c> identifiers. Each response uses
    ///     <c>{ErrorTypeBaseUri}/{URL-encoded errorCode}</c>. Trailing slashes are trimmed.
    ///     When null or whitespace, <c>type</c> is left unset unless another component sets it.
    /// </summary>
    public string? ErrorTypeBaseUri { get; set; }
}
