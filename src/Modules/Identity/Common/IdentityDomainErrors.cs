using ErrorOr;

namespace Identity.Errors;

internal static class IdentityDomainErrors
{
    internal static class Emails
    {
        internal static Error Empty()
        {
            return Error.Validation("EMAIL-0400-EMPTY", "Email cannot be empty.");
        }

        internal static Error InvalidFormat()
        {
            return Error.Validation("EMAIL-0400-FORMAT", "Invalid email format.");
        }

        internal static Error TooLong()
        {
            return Error.Validation("EMAIL-0400-LENGTH", "Email cannot exceed 320 characters.");
        }
    }

    internal static class Invitations
    {
        internal static Error Expired()
        {
            return Error.Conflict("INV-0410", "Invitation has expired.");
        }

        internal static Error AlreadyAccepted()
        {
            return Error.Conflict("INV-0409-ACCEPTED", "Invitation has already been accepted.");
        }

        internal static Error NotPending()
        {
            return Error.Conflict("INV-0409-NOT-PENDING", "Only pending invitations can be resent.");
        }

        internal static Error ExpiredCreateNew()
        {
            return Error.Conflict("INV-0410", "Invitation has expired. Create a new one instead.");
        }
    }
}
