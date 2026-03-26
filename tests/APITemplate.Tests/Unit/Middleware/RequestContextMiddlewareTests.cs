using APITemplate.Api.Middleware;
using APITemplate.Application.Common.Http;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Middleware;

public class RequestContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_EchoesCorrelationIdToResponse()
    {
        var middleware = new RequestContextMiddleware(async ctx =>
            await ctx.Response.WriteAsync("ok")
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[RequestContextConstants.Headers.CorrelationId] = "corr-123";

        await middleware.InvokeAsync(context);

        context
            .Response.Headers[RequestContextConstants.Headers.CorrelationId]
            .ToString()
            .ShouldBe("corr-123");
        context
            .Response.Headers[RequestContextConstants.Headers.TraceId]
            .ToString()
            .ShouldNotBeNullOrWhiteSpace();
        context
            .Response.Headers[RequestContextConstants.Headers.ElapsedMs]
            .ToString()
            .ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_UsesTraceIdentifierAsCorrelationId()
    {
        var middleware = new RequestContextMiddleware(async ctx =>
            await ctx.Response.WriteAsync("ok")
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-xyz";

        await middleware.InvokeAsync(context);

        context
            .Response.Headers[RequestContextConstants.Headers.CorrelationId]
            .ToString()
            .ShouldBe("trace-xyz");
    }
}
