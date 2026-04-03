using Identity.Features.User.DTOs;
using SharedKernel.Application.Validation;

namespace Identity.Features.User.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateUserRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateUserRequestValidator : DataAnnotationsValidator<CreateUserRequest> { }

