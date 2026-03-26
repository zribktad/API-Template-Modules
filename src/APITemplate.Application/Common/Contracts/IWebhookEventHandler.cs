using APITemplate.Application.Features.Examples.DTOs;

namespace APITemplate.Application.Common.Contracts;

/// <summary>
/// Strategy contract for processing a specific inbound webhook event type.
/// Implementations are discovered by type and selected at runtime based on the <see cref="EventType"/> they declare.
/// </summary>
public interface IWebhookEventHandler
{
    /// <summary>Gets the event-type string this handler is responsible for (e.g. <c>"order.created"</c>).</summary>
    string EventType { get; }

    /// <summary>Processes the inbound <paramref name="payload"/> for this event type.</summary>
    Task HandleAsync(WebhookPayload payload, CancellationToken ct = default);
}
