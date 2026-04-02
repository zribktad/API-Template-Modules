using ErrorOr;

namespace ProductCatalog.Application.Errors;

public static class DomainErrors
{
    public static class Products
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Products.NotFound,
                description: string.Format(ErrorCatalog.Products.NotFoundMessage, id)
            );
    }

    public static class Patch
    {
        public static Error InvalidPatchDocument(string message) =>
            Error.Validation(
                code: ErrorCatalog.Patch.InvalidDocument,
                description: string.Format(ErrorCatalog.Patch.InvalidDocumentMessage, message)
            );
    }

    public static class ProductData
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.ProductData.NotFound,
                description: string.Format(ErrorCatalog.ProductData.NotFoundMessage, id)
            );
    }

    public static class Categories
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Categories.NotFound,
                description: string.Format(ErrorCatalog.Categories.NotFoundMessage, id)
            );
    }
}
