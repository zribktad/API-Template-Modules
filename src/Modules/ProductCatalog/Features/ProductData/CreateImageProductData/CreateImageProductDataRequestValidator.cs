using FluentValidation;

namespace ProductCatalog.Features.ProductData.CreateImageProductData;

/// <summary>
///     FluentValidation validator for <see cref="CreateImageProductDataRequest" />.
/// </summary>
public sealed class CreateImageProductDataRequestValidator
    : AbstractValidator<CreateImageProductDataRequest>
{
    public CreateImageProductDataRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.Width)
            .GreaterThan(0)
            .WithMessage("Width must be greater than zero.");

        RuleFor(x => x.Height)
            .GreaterThan(0)
            .WithMessage("Height must be greater than zero.");

        RuleFor(x => x.Format)
            .NotEmpty()
            .WithMessage("Format is required.")
            .Must(format =>
                new[] { "jpg", "png", "gif", "webp" }.Contains(format, StringComparer.OrdinalIgnoreCase)
            )
            .WithMessage("Format must be one of: jpg, png, gif, webp.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("FileSizeBytes must be greater than zero.");
    }
}
