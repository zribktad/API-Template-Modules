using ErrorOr;

namespace Identity.Application.Errors;

public static class DomainErrors
{
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
}
