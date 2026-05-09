using System.Text.Json;
using Asp.Versioning;
using BuildingBlocks.Security;
using BuildingBlocks.Web.Api;
using Chatting.Features.GetNotificationStream;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Chatting.Features;

/// <summary>
/// Presentation-layer controller that demonstrates Server-Sent Events (SSE) by streaming
/// notifications as newline-delimited JSON over a persistent HTTP connection.
/// </summary>
[ApiVersion(1.0)]
public sealed class SseController(IMessageBus bus) : ApiControllerBase
{
    private const string EventStreamContentType = "text/event-stream";
    private const string NoCacheDirective = "no-cache";
    private const string KeepAliveConnection = "keep-alive";
    private const string SseDataPrefix = "data: ";

    [HttpGet("stream")]
    [RequirePermission(Permission.Examples.Read)]
    public async Task Stream([FromQuery] SseStreamRequest request, CancellationToken ct = default)
    {
        Response.ContentType = EventStreamContentType;
        Response.Headers.CacheControl = NoCacheDirective;
        Response.Headers.Connection = KeepAliveConnection;

        IAsyncEnumerable<SseNotificationItem> stream = await bus.InvokeAsync<
            IAsyncEnumerable<SseNotificationItem>
        >(new GetNotificationStreamQuery(request), ct);
        await using StreamWriter writer = new(Response.Body, leaveOpen: true);

        await foreach (SseNotificationItem item in stream.WithCancellation(ct))
        {
            string json = JsonSerializer.Serialize(item, JsonSerializerOptions.Web);
            await writer.WriteAsync($"{SseDataPrefix}{json}\n\n".AsMemory(), ct);
            await writer.FlushAsync(ct);
        }
    }
}
