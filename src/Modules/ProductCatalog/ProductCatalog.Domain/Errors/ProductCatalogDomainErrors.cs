using ErrorOr;

namespace ProductCatalog.Domain.Errors;

internal static class ProductCatalogDomainErrors
{
    internal static class Products
    {
        internal static Error NegativePrice() =>
            Error.Validation("PC-0400", "Price cannot be negative.");
    }
}
