using APITemplate.Api.Features.Examples.DTOs;
using SharedKernel.Application.Validation;

namespace APITemplate.Api.Features.Examples.Validation;

/// <summary>
/// FluentValidation validator for <see cref="IdempotentCreateRequest"/> that enforces
/// data-annotation constraints such as non-empty name and maximum length.
/// </summary>
public sealed class IdempotentCreateRequestValidator
    : DataAnnotationsValidator<IdempotentCreateRequest>;
