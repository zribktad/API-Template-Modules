namespace Identity.Errors;

public static class ErrorCatalog
{
    public static class KeycloakWebhook
    {
        public const string MissingUserId = "KC-WH-0400";
    }

    public static class Keycloak
    {
        public const string UserIdConflict = "KC-0409-USER-ID";
        public const string LocationMissing = "KC-0500-LOCATION";
        public const string EmptyUserId = "KC-0500-EMPTY-USER-ID";
        public const string EmptyToken = "KC-0500-EMPTY-TOKEN";
        public const string EmptyBody = "KC-0500-EMPTY-BODY";
        public const string CreateUserFailed = "KC-0500-CREATE-USER";
    }

    public static class Users
    {
        public const string NotFound = "USR-0404";
        public const string NotFoundByKeycloakId = "USR-0404-KC";
        public const string EmailAlreadyExists = "USR-0409-EMAIL";
        public const string UsernameAlreadyExists = "USR-0409-USERNAME";
        public const string CurrentPasswordInvalid = "USR-0403-PASSWORD";
        public const string KeycloakAccountRequired = "USR-0409-NO-KEYCLOAK";
        public const string NewPasswordMustDiffer = "USR-0422-PASSWORD-SAME";
    }

    public static class Roles
    {
        public const string NotFound = "ROL-0404";
        public const string Immutable = "ROL-0422-IMMUTABLE";
        public const string CannotGrantPlatformManage = "ROL-0403-PLATFORM";
        public const string CannotAssignForeignTenant = "ROL-0403-TENANT";
        public const string InvalidRoles = "ROL-0422-INVALID";
    }

    public static class Tenants
    {
        public const string NotFound = "TNT-0404";
        public const string CodeAlreadyExists = "TNT-0409-CODE";
        public const string CodeAlreadyExistsMessage = "Tenant with code '{0}' already exists.";
    }

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
}
