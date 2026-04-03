using ErrorOr;

namespace ProductCatalog.Common.Errors;

internal static class ProductCatalogDomainErrors
{
    internal static class Products
    {
        internal static Error NegativePrice() =>
            Error.Validation("PC-0400", "Price cannot be negative.");
    }
}
