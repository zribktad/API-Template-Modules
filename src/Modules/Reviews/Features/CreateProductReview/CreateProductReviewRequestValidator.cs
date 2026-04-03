using SharedKernel.Application.Validation;

namespace Reviews.Features;

/// <summary>
/// FluentValidation validator for <see cref="CreateProductReviewRequest"/>, delegating to data-annotation-based validation rules.
/// </summary>
public sealed class CreateProductReviewRequestValidator
    : DataAnnotationsValidator<CreateProductReviewRequest>;








