using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Examples.DTOs;

namespace APITemplate.Application.Features.Examples.Validation;

/// <summary>
/// FluentValidation validator for <see cref="SubmitJobRequest"/> that enforces data-annotation constraints, including required job type and optional URL format for the callback.
/// </summary>
public sealed class SubmitJobRequestValidator : DataAnnotationsValidator<SubmitJobRequest>;
