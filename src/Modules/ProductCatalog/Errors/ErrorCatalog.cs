namespace ProductCatalog.Errors;

public static class ErrorCatalog
{
    public static class Products
    {
        public const string EntityName = "Product";
        public const string NotFound = "PRD-0404";
        public const string NotFoundMessage = "Product '{0}' not found.";
        public const string ProductDataNotFound = "PRD-2404";
        public const string AlreadyExistsMessage = "Product '{0}' already exists.";
        public const string DuplicateIdMessage =
            "Duplicate product ID '{0}' appears multiple times in the request.";
    }

    public static class ProductData
    {
        public const string NotFound = "PDT-0404";
        public const string NotFoundMessage = "Product data not found: {0}";
        public const string InUse = "PDT-0409";
    }

    public static class Patch
    {
        public const string InvalidDocument = "PRD-0400-PATCH";
        public const string InvalidDocumentMessage = "Invalid patch document: {0}";
    }

    public static class Categories
    {
        public const string EntityName = "Category";
        public const string NotFound = "CAT-0404";
        public const string NotFoundMessage = "Category '{0}' not found.";
        public const string AlreadyExistsMessage = "Category '{0}' already exists.";
        public const string DuplicateIdMessage =
            "Duplicate category ID '{0}' appears multiple times in the request.";
    }
}

