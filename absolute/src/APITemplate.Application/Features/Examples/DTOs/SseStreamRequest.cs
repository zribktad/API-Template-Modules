using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Configuration request for the SSE notification stream, specifying how many events should be emitted (1–100).
/// </summary>
public sealed class SseStreamRequest
{
    [Range(1, 100)]
    public int Count { get; init; } = 5;
}
