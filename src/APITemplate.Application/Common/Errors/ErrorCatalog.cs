namespace APITemplate.Application.Common.Errors;

/// <summary>
/// Central catalog of structured error codes used throughout the Application and Presentation layers.
/// Organising codes here prevents duplication and makes it easy to cross-reference codes in API documentation.
/// </summary>
public static class ErrorCatalog
{
    /// <summary>Cross-cutting error codes not tied to a specific domain concept.</summary>
    public static class General
    {
        public const string Unknown = "GEN-0001";
        public const string ValidationFailed = "GEN-0400";
        public const string PageOutOfRange = "GEN-0400-PAGE";
        public const string NotFound = "GEN-0404";
        public const string Conflict = "GEN-0409";
        public const string ConcurrencyConflict = "GEN-0409-CONCURRENCY";
    }

    /// <summary>Error codes for authentication and authorisation failures.</summary>
    public static class Auth
    {
        public const string Forbidden = "AUTH-0403";
        public const string ForbiddenOwnReviewsOnly = "You can only delete your own reviews.";
    }

    /// <summary>Error codes specific to the Products domain.</summary>
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

    /// <summary>Error codes specific to the ProductData domain.</summary>
    public static class ProductData
    {
        public const string NotFound = "PDT-0404";
        public const string NotFoundMessage = "Product data not found: {0}";
        public const string InUse = "PDT-0409";
    }

    /// <summary>Error codes specific to the Categories domain.</summary>
    public static class Categories
    {
        public const string EntityName = "Category";
        public const string NotFound = "CAT-0404";
        public const string NotFoundMessage = "Category '{0}' not found.";
        public const string AlreadyExistsMessage = "Category '{0}' already exists.";
        public const string DuplicateIdMessage =
            "Duplicate category ID '{0}' appears multiple times in the request.";
    }

    /// <summary>Error codes specific to the Reviews domain.</summary>
    public static class Reviews
    {
        public const string ProductNotFoundForReview = "REV-2101";
        public const string ReviewNotFound = "REV-0404";
    }

    /// <summary>Error codes specific to the Users domain.</summary>
    public static class Users
    {
        public const string NotFound = "USR-0404";
        public const string EmailAlreadyExists = "USR-0409-EMAIL";
        public const string UsernameAlreadyExists = "USR-0409-USERNAME";
    }

    /// <summary>Error codes specific to the Tenants domain.</summary>
    public static class Tenants
    {
        public const string NotFound = "TNT-0404";
        public const string CodeAlreadyExists = "TNT-0409-CODE";
        public const string CodeAlreadyExistsMessage = "Tenant with code '{0}' already exists.";
    }

    /// <summary>Error codes specific to the Invitations domain.</summary>
    public static class Invitations
    {
        public const string NotFound = "INV-0404";
        public const string AlreadyPending = "INV-0409-PENDING";
        public const string Expired = "INV-0410";
        public const string AlreadyAccepted = "INV-0409-ACCEPTED";
        public const string NotPending = "INV-0409-NOT-PENDING";

        public const string NotFoundOrExpiredMessage = "Invitation not found or expired.";
        public const string ExpiredMessage = "Invitation has expired.";
        public const string AlreadyAcceptedMessage = "Invitation has already been accepted.";
        public const string NotPendingMessage = "Only pending invitations can be resent.";
        public const string ExpiredCreateNewMessage =
            "Invitation has expired. Create a new one instead.";
    }

    /// <summary>Error codes used by the example/showcase feature endpoints.</summary>
    public static class Examples
    {
        public const string FileNotFound = "EXA-0404-FILE";
        public const string InvalidFileType = "EXA-0400-FILE";
        public const string FileTooLarge = "EXA-0400-SIZE";
        public const string InvalidPatchDocument = "EXA-0400-PATCH";
        public const string WebhookInvalidSignature = "EXA-0401-WEBHOOK";
        public const string WebhookMissingHeaders = "EXA-0401-WEBHOOK-HDR";
    }
}
