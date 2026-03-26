using ErrorOr;

namespace APITemplate.Application.Common.Errors;

/// <summary>
/// Factory methods producing <see cref="Error"/> instances that mirror the
/// <see cref="ErrorCatalog"/> codes. Each method sets the appropriate <see cref="ErrorType"/>
/// so the presentation layer can map them to HTTP status codes without domain knowledge.
/// </summary>
public static class DomainErrors
{
    public static class General
    {
        public static Error NotFound(string entityName, Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.General.NotFound,
                description: $"{entityName} with id '{id}' not found."
            );
    }

    public static class Auth
    {
        public static Error ForbiddenOwnReviewsOnly() =>
            Error.Forbidden(
                code: ErrorCatalog.Auth.Forbidden,
                description: ErrorCatalog.Auth.ForbiddenOwnReviewsOnly
            );
    }

    public static class Products
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Products.NotFound,
                description: string.Format(ErrorCatalog.Products.NotFoundMessage, id)
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

    public static class Reviews
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Reviews.ReviewNotFound,
                description: $"Review with id '{id}' not found."
            );

        public static Error ProductNotFoundForReview(Guid productId) =>
            Error.NotFound(
                code: ErrorCatalog.Reviews.ProductNotFoundForReview,
                description: $"Product with id '{productId}' not found."
            );
    }

    public static class Users
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Users.NotFound,
                description: $"User with id '{id}' not found."
            );

        public static Error EmailAlreadyExists(string email) =>
            Error.Conflict(
                code: ErrorCatalog.Users.EmailAlreadyExists,
                description: $"Email '{email}' is already in use."
            );

        public static Error UsernameAlreadyExists(string username) =>
            Error.Conflict(
                code: ErrorCatalog.Users.UsernameAlreadyExists,
                description: $"Username '{username}' is already in use."
            );
    }

    public static class Tenants
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Tenants.NotFound,
                description: $"Tenant with id '{id}' not found."
            );

        public static Error CodeAlreadyExists(string code) =>
            Error.Conflict(
                code: ErrorCatalog.Tenants.CodeAlreadyExists,
                description: string.Format(ErrorCatalog.Tenants.CodeAlreadyExistsMessage, code)
            );
    }

    public static class Invitations
    {
        public static Error NotFound(Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.Invitations.NotFound,
                description: $"Invitation with id '{id}' not found."
            );

        public static Error AlreadyPending(string email) =>
            Error.Conflict(
                code: ErrorCatalog.Invitations.AlreadyPending,
                description: $"A pending invitation for '{email}' already exists."
            );

        public static Error Expired() =>
            Error.Conflict(
                code: ErrorCatalog.Invitations.Expired,
                description: ErrorCatalog.Invitations.ExpiredMessage
            );

        public static Error ExpiredCreateNew() =>
            Error.Conflict(
                code: ErrorCatalog.Invitations.Expired,
                description: ErrorCatalog.Invitations.ExpiredCreateNewMessage
            );

        public static Error AlreadyAccepted() =>
            Error.Conflict(
                code: ErrorCatalog.Invitations.AlreadyAccepted,
                description: ErrorCatalog.Invitations.AlreadyAcceptedMessage
            );

        public static Error NotPending() =>
            Error.Conflict(
                code: ErrorCatalog.Invitations.NotPending,
                description: ErrorCatalog.Invitations.NotPendingMessage
            );

        public static Error NotFoundOrExpired() =>
            Error.NotFound(
                code: ErrorCatalog.Invitations.NotFound,
                description: ErrorCatalog.Invitations.NotFoundOrExpiredMessage
            );
    }

    public static class Examples
    {
        public static Error FileNotFound(string fileName) =>
            Error.NotFound(
                code: ErrorCatalog.Examples.FileNotFound,
                description: $"File '{fileName}' not found."
            );

        public static Error InvalidFileType(string extension) =>
            Error.Validation(
                code: ErrorCatalog.Examples.InvalidFileType,
                description: $"File type '{extension}' is not allowed."
            );

        public static Error FileTooLarge(long maxSize) =>
            Error.Validation(
                code: ErrorCatalog.Examples.FileTooLarge,
                description: $"File exceeds maximum size of {maxSize} bytes."
            );

        public static Error InvalidPatchDocument(string message) =>
            Error.Validation(
                code: ErrorCatalog.Examples.InvalidPatchDocument,
                description: message
            );

        public static Error WebhookInvalidSignature() =>
            Error.Unauthorized(
                code: ErrorCatalog.Examples.WebhookInvalidSignature,
                description: "Invalid webhook signature."
            );

        public static Error WebhookMissingHeaders() =>
            Error.Unauthorized(
                code: ErrorCatalog.Examples.WebhookMissingHeaders,
                description: "Required webhook headers are missing."
            );
    }
}
