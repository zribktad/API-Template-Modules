using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.User.DTOs;

namespace APITemplate.Application.Features.User.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateUserRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateUserRequestValidator : DataAnnotationsValidator<CreateUserRequest> { }
