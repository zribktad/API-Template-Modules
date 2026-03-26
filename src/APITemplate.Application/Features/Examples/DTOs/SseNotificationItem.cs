namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Represents a single Server-Sent Events (SSE) notification item emitted by the stream, carrying a sequence number, message text, and UTC timestamp.
/// </summary>
public sealed record SseNotificationItem(int Sequence, string Message, DateTime TimestampUtc);
