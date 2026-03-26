using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Carries the data for an idempotent resource creation request; demonstrates safe-retry semantics at the API layer.
/// </summary>
public sealed record IdempotentCreateRequest(
    [NotEmpty] [MaxLength(200)] string Name,
    string? Description
);
