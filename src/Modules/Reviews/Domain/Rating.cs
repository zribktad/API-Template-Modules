using ErrorOr;

namespace Reviews.Domain;

/// <summary>
///     Value object representing a product review rating in the range 1–5 (inclusive).
/// </summary>
public readonly record struct Rating
{
    private Rating(int value)
    {
        Value = value;
    }

    public int Value { get; }

    /// <summary>Creates a <see cref="Rating" /> after validating that <paramref name="value" /> is between 1 and 5.</summary>
    public static ErrorOr<Rating> Create(int value)
    {
        if (value is < 1 or > 5)
            return ReviewsDomainErrors.Rating.OutOfRange();

        return new Rating(value);
    }

    /// <summary>Factory method for EF Core use only. Bypasses validation as values come from persistence.</summary>
    public static Rating FromPersistence(int value)
    {
        return new Rating(value);
    }

    public static implicit operator int(Rating rating)
    {
        return rating.Value;
    }
}
