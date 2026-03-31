using Identity.Application.Features.User.DTOs;
using SharedKernel.Application.Validation;

namespace Identity.Application.Features.User.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateUserRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateUserRequestValidator : DataAnnotationsValidator<CreateUserRequest> { }
