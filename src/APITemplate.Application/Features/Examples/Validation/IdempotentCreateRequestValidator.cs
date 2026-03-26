using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Examples.DTOs;

namespace APITemplate.Application.Features.Examples.Validation;

/// <summary>
/// FluentValidation validator for <see cref="IdempotentCreateRequest"/> that enforces data-annotation constraints such as non-empty name and maximum length.
/// </summary>
public sealed class IdempotentCreateRequestValidator
    : DataAnnotationsValidator<IdempotentCreateRequest>;
