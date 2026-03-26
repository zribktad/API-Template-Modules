using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.ProductReview.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateProductReviewRequest"/>, delegating to data-annotation-based validation rules.
/// </summary>
public sealed class CreateProductReviewRequestValidator
    : DataAnnotationsValidator<CreateProductReviewRequest>;
