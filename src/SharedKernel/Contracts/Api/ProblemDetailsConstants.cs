namespace SharedKernel.Contracts.Api;

/// <summary>
///     Common extension keys for RFC 7807 <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" />.
/// </summary>
public static class ProblemDetailsConstants
{
    public const string TraceId = "traceId";
    public const string ErrorCode = "errorCode";
    public const string Errors = "errors";
    public const string Metadata = "metadata";
}
