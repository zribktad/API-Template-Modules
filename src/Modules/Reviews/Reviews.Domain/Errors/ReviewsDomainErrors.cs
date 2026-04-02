using ErrorOr;

namespace Reviews.Domain.Errors;

internal static class ReviewsDomainErrors
{
    internal static class Rating
    {
        internal static Error OutOfRange() =>
            Error.Validation("REV-0401", "Rating must be between 1 and 5.");
    }
}
