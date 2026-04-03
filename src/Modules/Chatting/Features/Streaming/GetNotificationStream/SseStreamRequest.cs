using System.ComponentModel.DataAnnotations;

namespace Chatting.Features.Streaming.GetNotificationStream;

/// <summary>
/// Configuration request for the SSE notification stream, specifying how many events should be emitted (1–100).
/// </summary>
public sealed class SseStreamRequest
{
    [Range(1, 100)]
    public int Count { get; init; } = 5;
}

