namespace SharedKernel.Application.Errors;

/// <summary>
///     Cross-cutting error codes shared by multiple modules.
///     Module-specific error codes live in each module's own Errors/ErrorCatalog.cs.
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

    /// <summary>Generic authentication and authorisation error codes.</summary>
    public static class Auth
    {
        public const string Unauthorized = "AUTH-0401";
        public const string Forbidden = "AUTH-0403";
    }
}
