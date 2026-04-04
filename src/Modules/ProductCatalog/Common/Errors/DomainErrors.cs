namespace ProductCatalog.Common.Errors;

public static class DomainErrors
{
    public static class Products
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(
                ErrorCatalog.Products.NotFound,
                string.Format(ErrorCatalog.Products.NotFoundMessage, id)
            );
        }
    }

    public static class Patch
    {
        public static Error InvalidPatchDocument(string message)
        {
            return Error.Validation(
                ErrorCatalog.Patch.InvalidDocument,
                string.Format(ErrorCatalog.Patch.InvalidDocumentMessage, message)
            );
        }
    }

    public static class ProductData
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(
                ErrorCatalog.ProductData.NotFound,
                string.Format(ErrorCatalog.ProductData.NotFoundMessage, id)
            );
        }
    }

    public static class Categories
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(
                ErrorCatalog.Categories.NotFound,
                string.Format(ErrorCatalog.Categories.NotFoundMessage, id)
            );
        }
    }
}
