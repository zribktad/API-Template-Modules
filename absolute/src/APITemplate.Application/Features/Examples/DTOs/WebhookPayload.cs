using System.Text.Json;

namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Represents an incoming webhook payload with a discriminated event type, a unique event ID for deduplication, and a raw JSON data element.
/// </summary>
public sealed record WebhookPayload(string EventType, string EventId, JsonElement Data);
