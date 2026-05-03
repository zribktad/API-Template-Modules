using BuildingBlocks.Application.Http;
using Serilog.Context;

namespace APITemplate.Api.Middleware;

/// <summary>
///     Runs early in the pipeline: stores correlation id and enriches Serilog without writing to
///     <see cref="HttpResponse" /> so authentication challenges can set status codes safely.
/// </summary>
public sealed class CorrelationContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = RequestCorrelationHelper.ResolveCorrelationId(context);
        context.Items[RequestContextConstants.ContextKeys.CorrelationId] = correlationId;

        using (
            LogContext.PushProperty(
                RequestContextConstants.LogProperties.CorrelationId,
                correlationId
            )
        )
            await next(context);
    }
}
