namespace Identity.Application.Errors;

public static class ErrorCatalog
{
    public static class Users
    {
        public const string NotFound = "USR-0404";
        public const string EmailAlreadyExists = "USR-0409-EMAIL";
        public const string UsernameAlreadyExists = "USR-0409-USERNAME";
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
