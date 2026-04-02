using ErrorOr;

namespace Identity.Domain.Errors;

internal static class IdentityDomainErrors
{
    internal static class TenantCodes
    {
        internal static Error Empty() =>
            Error.Validation("TC-0400-EMPTY", "Tenant code cannot be empty.");

        internal static Error TooLong() =>
            Error.Validation("TC-0400-LENGTH", "Tenant code cannot exceed 100 characters.");
    }

    internal static class Invitations
    {
        internal static Error Expired() => Error.Conflict("INV-0410", "Invitation has expired.");

        internal static Error AlreadyAccepted() =>
            Error.Conflict("INV-0409-ACCEPTED", "Invitation has already been accepted.");
    }
}
