using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Carries the parameters needed to enqueue a new background job, including an optional JSON parameters string and an optional webhook callback URL.
/// </summary>
public sealed record SubmitJobRequest(
    [NotEmpty(ErrorMessage = "Job type is required.")] [MaxLength(100)] string JobType,
    string? Parameters = null,
    [Url] [MaxLength(2048)] string? CallbackUrl = null
);
