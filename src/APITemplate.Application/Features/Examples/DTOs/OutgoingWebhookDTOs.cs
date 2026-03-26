namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Represents a pending outgoing webhook delivery, pairing the destination URL with the pre-serialised JSON payload.
/// </summary>
public sealed record OutgoingWebhookItem(string CallbackUrl, string SerializedPayload);

/// <summary>
/// The strongly-typed payload delivered to a webhook callback URL upon job completion, carrying final status, result, and error information.
/// </summary>
public sealed record OutgoingJobWebhookPayload(
    Guid JobId,
    string JobType,
    string Status,
    string? ResultPayload,
    string? ErrorMessage,
    DateTime CompletedAtUtc
);
