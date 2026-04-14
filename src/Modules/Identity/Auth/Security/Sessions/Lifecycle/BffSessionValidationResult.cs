namespace Identity.Auth.Security.Sessions.Lifecycle;

public enum BffSessionValidationAction
{
    Accept = 0,
    Reject = 1,
    Expire = 2,
    Revoke = 3,
}

public readonly record struct BffSessionValidationResult(
    BffSessionValidationAction Action,
    BffSessionRevocationReason? RevocationReason = null
)
{
    public static BffSessionValidationResult Accept() =>
        new(BffSessionValidationAction.Accept);

    public static BffSessionValidationResult Reject() =>
        new(BffSessionValidationAction.Reject);

    public static BffSessionValidationResult Expire() =>
        new(BffSessionValidationAction.Expire);

    public static BffSessionValidationResult Revoke(BffSessionRevocationReason reason) =>
        new(BffSessionValidationAction.Revoke, reason);
}
