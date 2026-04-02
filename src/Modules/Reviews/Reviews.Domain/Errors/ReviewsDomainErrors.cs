using ErrorOr;

namespace Reviews.Domain.Errors;

internal static class ReviewsDomainErrors
{
    internal static class Rating
    {
        internal static Error OutOfRange() =>
            Error.Validation("RATING-0001", "Rating must be between 1 and 5.");
    }
}
