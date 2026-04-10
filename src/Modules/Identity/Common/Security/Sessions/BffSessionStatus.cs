namespace Identity.Security.Sessions;

/// <summary>
///     Represents the lifecycle state of a server-side BFF session record.
/// </summary>
public enum BffSessionStatus
{
    /// <summary>The session is active and may be used for authentication.</summary>
    Active = 0,

    /// <summary>A refresh operation is currently in progress for the session.</summary>
    Refreshing = 1,

    /// <summary>The session has been explicitly revoked and must no longer authenticate.</summary>
    Revoked = 2,

    /// <summary>The session expired due to lifecycle limits.</summary>
    Expired = 3,
}
