using ErrorOr;
using ProductCatalog.Errors;

namespace ProductCatalog.ValueObjects;

/// <summary>
/// Value object representing a product price. Must be non-negative.
/// </summary>
public readonly record struct Price
{
    public decimal Value { get; }

    private Price(decimal value) => Value = value;

    /// <summary>Creates a <see cref="Price"/> after validating that <paramref name="value"/> is non-negative.</summary>
    public static ErrorOr<Price> Create(decimal value)
    {
        if (value < 0)
            return ProductCatalogDomainErrors.Products.NegativePrice();

        return new Price(value);
    }

    /// <summary>Factory method for EF Core use only. Bypasses validation as values come from persistence.</summary>
    public static Price FromPersistence(decimal value) => new(value);

    /// <summary>Represents a zero price (e.g. free products).</summary>
    public static Price Zero => new(0);

    public static implicit operator decimal(Price price) => price.Value;
}

