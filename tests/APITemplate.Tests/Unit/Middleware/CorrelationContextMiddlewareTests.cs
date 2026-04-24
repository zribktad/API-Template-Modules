using APITemplate.Api.Middleware;
using Microsoft.AspNetCore.Http;
using SharedKernel.Application.Http;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Middleware;

[Trait("Category", "Unit")]
public class CorrelationContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsItemsAndDoesNotSetResponseHeaders()
    {
        CorrelationContextMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[RequestContextConstants.Headers.CorrelationId] = "corr-early";

        await middleware.InvokeAsync(context);

        context.Items[RequestContextConstants.ContextKeys.CorrelationId].ShouldBe("corr-early");
        context.Response.Headers.Count.ShouldBe(0);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_UsesTraceIdentifierInItems()
    {
        CorrelationContextMiddleware middleware = new(async ctx =>
            await ctx.Response.WriteAsync("ok")
        );
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-early";

        await middleware.InvokeAsync(context);

        context.Items[RequestContextConstants.ContextKeys.CorrelationId].ShouldBe("trace-early");
    }
}
