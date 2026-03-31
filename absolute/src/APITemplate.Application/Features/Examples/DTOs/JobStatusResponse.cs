using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Represents the full runtime state of a background job, including progress, result payload, error information, and optional webhook callback URL.
/// </summary>
public sealed record JobStatusResponse(
    Guid Id,
    string JobType,
    JobStatus Status,
    int ProgressPercent,
    string? Parameters,
    string? ResultPayload,
    string? ErrorMessage,
    DateTime SubmittedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? CallbackUrl
) : IHasId;
