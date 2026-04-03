using System.Text.Json;
using Asp.Versioning;
using Chatting.Application.Features.Streaming.DTOs;
using Chatting.Application.Features.Streaming.Queries;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace Chatting.Api.Controllers.V1;

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
            await writer.WriteAsync($"{SseDataPrefix}{json}\n\n");
            await writer.FlushAsync(ct);
        }
    }
}
