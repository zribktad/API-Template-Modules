using ErrorOr;

namespace Reviews.Common.Errors;

public static class DomainErrors
{
    public static class Reviews
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(
                ErrorCatalog.Reviews.ReviewNotFound,
                $"Review with id '{id}' not found."
            );
        }

        public static Error ProductNotFoundForReview(Guid productId)
        {
            return Error.NotFound(
                ErrorCatalog.Reviews.ProductNotFoundForReview,
                $"Product with id '{productId}' not found."
            );
        }

        public static Error ForbiddenOwnReviewsOnly()
        {
            return Error.Forbidden(
                SharedKernel.Application.Errors.ErrorCatalog.Auth.Forbidden,
                ErrorCatalog.Reviews.ForbiddenOwnReviewsOnlyMessage
            );
        }
    }
}
