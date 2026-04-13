using ErrorOr;

namespace Identity.Errors;

public static class DomainErrors
{
    public static class Users
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(ErrorCatalog.Users.NotFound, $"User with id '{id}' not found.");
        }

        public static Error NotFoundByKeycloakUserId(string keycloakUserId)
        {
            return Error.NotFound(
                ErrorCatalog.Users.NotFoundByKeycloakId,
                $"User with Keycloak id '{keycloakUserId}' not found."
            );
        }

        public static Error EmailAlreadyExists(string email)
        {
            return Error.Conflict(
                ErrorCatalog.Users.EmailAlreadyExists,
                $"Email '{email}' is already in use."
            );
        }

        public static Error UsernameAlreadyExists(string username)
        {
            return Error.Conflict(
                ErrorCatalog.Users.UsernameAlreadyExists,
                $"Username '{username}' is already in use."
            );
        }

        public static Error CurrentPasswordInvalid()
        {
            return Error.Validation(
                ErrorCatalog.Users.CurrentPasswordInvalid,
                "The current password is not correct."
            );
        }

        public static Error KeycloakAccountRequired()
        {
            return Error.Validation(
                ErrorCatalog.Users.KeycloakAccountRequired,
                "This account is not linked to Keycloak; password cannot be changed here."
            );
        }

        public static Error NewPasswordMustDiffer()
        {
            return Error.Validation(
                ErrorCatalog.Users.NewPasswordMustDiffer,
                "The new password must be different from the current password."
            );
        }
    }

    public static class Roles
    {
        public static Error CannotAssignForeignTenant()
        {
            return Error.Forbidden(
                ErrorCatalog.Roles.CannotAssignForeignTenant,
                "Cannot assign roles that belong to another tenant."
            );
        }
    }

    public static class Tenants
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(
                ErrorCatalog.Tenants.NotFound,
                $"Tenant with id '{id}' not found."
            );
        }

        public static Error CodeAlreadyExists(string code)
        {
            return Error.Conflict(
                ErrorCatalog.Tenants.CodeAlreadyExists,
                string.Format(ErrorCatalog.Tenants.CodeAlreadyExistsMessage, code)
            );
        }
    }

    public static class Invitations
    {
        public static Error NotFound(Guid id)
        {
            return Error.NotFound(
                ErrorCatalog.Invitations.NotFound,
                $"Invitation with id '{id}' not found."
            );
        }

        public static Error AlreadyPending(string email)
        {
            return Error.Conflict(
                ErrorCatalog.Invitations.AlreadyPending,
                $"A pending invitation for '{email}' already exists."
            );
        }

        public static Error Expired()
        {
            return Error.Conflict(
                ErrorCatalog.Invitations.Expired,
                ErrorCatalog.Invitations.ExpiredMessage
            );
        }

        public static Error ExpiredCreateNew()
        {
            return Error.Conflict(
                ErrorCatalog.Invitations.Expired,
                ErrorCatalog.Invitations.ExpiredCreateNewMessage
            );
        }

        public static Error AlreadyAccepted()
        {
            return Error.Conflict(
                ErrorCatalog.Invitations.AlreadyAccepted,
                ErrorCatalog.Invitations.AlreadyAcceptedMessage
            );
        }

        public static Error NotPending()
        {
            return Error.Conflict(
                ErrorCatalog.Invitations.NotPending,
                ErrorCatalog.Invitations.NotPendingMessage
            );
        }

        public static Error NotFoundOrExpired()
        {
            return Error.NotFound(
                ErrorCatalog.Invitations.NotFound,
                ErrorCatalog.Invitations.NotFoundOrExpiredMessage
            );
        }
    }
}
