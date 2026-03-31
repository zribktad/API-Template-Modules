using System.Text.Json;
using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Application.Features.Examples;
using APITemplate.Application.Features.Examples.DTOs;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that demonstrates Server-Sent Events (SSE) by streaming
/// notifications as newline-delimited JSON over a persistent HTTP connection.
/// </summary>
public sealed class SseController(IMessageBus bus) : ApiControllerBase
{
    private const string EventStreamContentType = "text/event-stream";
    private const string NoCacheDirective = "no-cache";
    private const string KeepAliveConnection = "keep-alive";
    private const string SseDataPrefix = "data: ";

    /// <summary>
    /// Sets SSE response headers and then iterates an async notification stream, writing each
    /// item as a <c>data: &lt;json&gt;\n\n</c> frame and flushing immediately for low latency.
    /// </summary>
    [HttpGet("stream")]
    [RequirePermission(Permission.Examples.Read)]
    public async Task Stream([FromQuery] SseStreamRequest request, CancellationToken ct = default)
    {
        Response.ContentType = EventStreamContentType;
        Response.Headers.CacheControl = NoCacheDirective;
        Response.Headers.Connection = KeepAliveConnection;

        var stream = await bus.InvokeAsync<IAsyncEnumerable<SseNotificationItem>>(
            new GetNotificationStreamQuery(request),
            ct
        );
        await using var writer = new StreamWriter(Response.Body, leaveOpen: true);

        await foreach (var item in stream.WithCancellation(ct))
        {
            var json = JsonSerializer.Serialize(item, JsonSerializerOptions.Web);
            await writer.WriteAsync($"{SseDataPrefix}{json}\n\n");
            await writer.FlushAsync(ct);
        }
    }
}
