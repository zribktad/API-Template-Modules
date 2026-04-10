namespace Identity.Security.Sessions;

/// <summary>
///     Result object returned by the BFF refresh pipeline, describing whether refresh succeeded
///     and whether the cookie ticket should be renewed.
/// </summary>
public sealed class BffRefreshOutcome
{
    private BffRefreshOutcome() { }

    /// <summary>
    ///     Creates a successful outcome indicating that the current session remains valid and the
    ///     cookie does not need renewal.
    /// </summary>
    public static BffRefreshOutcome NotRequired(BffSessionRecord session) =>
        new()
        {
            RequiresRenewal = false,
            Succeeded = true,
            Session = session,
        };

    /// <summary>
    ///     Creates a successful outcome indicating that fresh session material is available and
    ///     the cookie should be renewed.
    /// </summary>
    public static BffRefreshOutcome Success(BffSessionRecord session) =>
        new()
        {
            RequiresRenewal = true,
            Succeeded = true,
            Session = session,
        };

    /// <summary>
    ///     Creates a failed outcome with the revocation or rejection reason that prevented refresh.
    /// </summary>
    public static BffRefreshOutcome Failed(BffSessionRevocationReason reason) =>
        new()
        {
            RequiresRenewal = false,
            Succeeded = false,
            FailureReason = reason,
        };

    /// <summary>
    ///     Gets a value indicating whether the authentication cookie should be re-issued.
    /// </summary>
    public bool RequiresRenewal { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether refresh completed successfully.
    /// </summary>
    public bool Succeeded { get; private init; }

    /// <summary>
    ///     Gets the current session snapshot when refresh succeeded.
    /// </summary>
    public BffSessionRecord? Session { get; private init; }

    /// <summary>
    ///     Gets the failure reason when refresh did not succeed.
    /// </summary>
    public BffSessionRevocationReason? FailureReason { get; private init; }
}
