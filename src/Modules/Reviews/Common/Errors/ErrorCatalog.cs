namespace Reviews.Common.Errors;

public static class ErrorCatalog
{
    public static class Reviews
    {
        public const string ProductNotFoundForReview = "REV-2101";
        public const string ReviewNotFound = "REV-0404";

        public const string ForbiddenOwnReviewsOnlyMessage =
            "You can only delete your own reviews.";
    }
}
