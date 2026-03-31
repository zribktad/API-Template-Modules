namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Carries the unique identifier of the background job whose status is being queried.
/// </summary>
public sealed record GetJobStatusRequest(Guid Id) : IHasId;
