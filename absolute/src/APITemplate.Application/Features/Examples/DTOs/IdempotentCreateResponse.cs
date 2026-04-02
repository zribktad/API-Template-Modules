namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Represents the persisted resource returned after a successful idempotent create operation.
/// </summary>
public sealed record IdempotentCreateResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc
) : IHasId;
