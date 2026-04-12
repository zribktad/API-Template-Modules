namespace Identity.Directory.Features.User;

/// <summary>
///     FluentValidation validator for <see cref="CreateUserRequest" /> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateUserRequestValidator : DataAnnotationsValidator<CreateUserRequest> { }
