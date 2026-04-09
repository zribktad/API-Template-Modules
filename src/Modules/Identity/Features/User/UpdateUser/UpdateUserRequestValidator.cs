namespace Identity.Features.User;

/// <summary>
///     FluentValidation validator for <see cref="UpdateUserRequest" /> that enforces data-annotation constraints.
/// </summary>
public sealed class UpdateUserRequestValidator : DataAnnotationsValidator<UpdateUserRequest>;
