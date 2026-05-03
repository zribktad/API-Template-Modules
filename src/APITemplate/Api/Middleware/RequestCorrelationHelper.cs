using BuildingBlocks.Application.Http;

namespace APITemplate.Api.Middleware;

/// <summary>
///     Shared correlation id resolution for request pipeline middleware.
/// </summary>
internal static class RequestCorrelationHelper
{
    public static string ResolveCorrelationId(HttpContext context)
    {
        string incoming = context
            .Request.Headers[RequestContextConstants.Headers.CorrelationId]
            .ToString();
        return string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming;
    }
}
