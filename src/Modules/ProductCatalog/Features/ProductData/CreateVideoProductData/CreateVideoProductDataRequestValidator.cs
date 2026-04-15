using FluentValidation;

namespace ProductCatalog.Features.ProductData.CreateVideoProductData;

/// <summary>
///     FluentValidation validator for <see cref="CreateVideoProductDataRequest" />.
/// </summary>
public sealed class CreateVideoProductDataRequestValidator
    : AbstractValidator<CreateVideoProductDataRequest>
{
    public CreateVideoProductDataRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0)
            .WithMessage("DurationSeconds must be greater than zero.");

        RuleFor(x => x.Resolution)
            .NotEmpty()
            .WithMessage("Resolution is required.")
            .Must(resolution =>
                new[] { "720p", "1080p", "4K" }.Contains(resolution, StringComparer.OrdinalIgnoreCase)
            )
            .WithMessage("Resolution must be one of: 720p, 1080p, 4K.");

        RuleFor(x => x.Format)
            .NotEmpty()
            .WithMessage("Format is required.")
            .Must(format =>
                new[] { "mp4", "avi", "mkv" }.Contains(format, StringComparer.OrdinalIgnoreCase)
            )
            .WithMessage("Format must be one of: mp4, avi, mkv.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("FileSizeBytes must be greater than zero.");
    }
}
