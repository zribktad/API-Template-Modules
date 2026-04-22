namespace Identity.Auth.Security;

/// <summary>
///     Carries a machine-readable <see cref="ErrorCode" /> when authentication succeeds at the IdP
///     but the application rejects the principal (invitation / local user gate).
/// </summary>
public sealed class UserAccessDeniedException : Exception
{
    public string ErrorCode { get; }

    public UserAccessDeniedException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
