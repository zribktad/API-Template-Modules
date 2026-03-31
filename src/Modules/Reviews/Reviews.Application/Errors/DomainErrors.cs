using ErrorOr;

namespace Reviews.Application.Errors;

public static class DomainErrors
{
    public static class Reviews
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Reviews.ReviewNotFound,
                description: $"Review with id '{id}' not found."
            );

        public static Error ProductNotFoundForReview(Guid productId) =>
            Error.NotFound(
                code: ErrorCatalog.Reviews.ProductNotFoundForReview,
                description: $"Product with id '{productId}' not found."
            );

        public static Error ForbiddenOwnReviewsOnly() =>
            Error.Forbidden(
                code: "AUTH-0403",
                description: ErrorCatalog.Reviews.ForbiddenOwnReviewsOnly
            );
    }
}
