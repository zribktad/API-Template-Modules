using System.ComponentModel.DataAnnotations;
using ErrorOr;
using SharedKernel.Application.Validation;
using SharedKernelErrorCatalog = SharedKernel.Application.Errors.ErrorCatalog;

namespace Reviews.Features;

/// <summary>Returns a paginated, filtered, and sorted list of product reviews.</summary>
public sealed record GetProductReviewsQuery(ProductReviewFilter Filter);

/// <summary>Handles <see cref="GetProductReviewsQuery" />.</summary>
public sealed class GetProductReviewsQueryHandler
{
    public static ErrorOr<Success> Validate(GetProductReviewsQuery query, IValidator validator)
    {
        IReadOnlyList<ValidationResult> failures = validator.Validate(query.Filter);
        if (failures.Count == 0)
            return Result.Success;
        return failures
            .Select(f => Error.Validation(
                SharedKernelErrorCatalog.General.ValidationFailed,
                f.ErrorMessage ?? "Validation failed.",
                new Dictionary<string, object> { ["propertyName"] = string.Join(", ", f.MemberNames) }))
            .ToList<Error>();
    }

    public static async Task<ErrorOr<PagedResponse<ProductReviewResponse>>> HandleAsync(
        GetProductReviewsQuery request,
        ErrorOr<Success> validation,
        IProductReviewRepository reviewRepository,
        CancellationToken ct
    )
    {
        if (validation.IsError)
            return validation.Errors;

        return await reviewRepository.GetPagedAsync(
            new ProductReviewSpecification(request.Filter),
            request.Filter.PageNumber,
            request.Filter.PageSize,
            ct
        );
    }
}
