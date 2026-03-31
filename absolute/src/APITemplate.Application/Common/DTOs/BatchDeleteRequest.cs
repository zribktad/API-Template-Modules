using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.DTOs;

/// <summary>
/// Carries a list of entity identifiers to be deleted in a single batch operation; accepts between 1 and 100 IDs.
/// </summary>
public sealed record BatchDeleteRequest(
    [MinLength(1, ErrorMessage = "At least one ID is required.")]
    [MaxLength(100, ErrorMessage = "Maximum 100 IDs per batch.")]
        IReadOnlyList<Guid> Ids
);
