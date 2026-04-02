namespace APITemplate.Api.Filters.Idempotency;

/// <summary>
/// Shared constants for the idempotency feature: header name, key constraints, and default timeouts.
/// </summary>
public static class IdempotencyConstants
{
    public const string HeaderName = "Idempotency-Key";
    public const int DefaultTtlHours = 24;
    public const int LockTimeoutSeconds = 30;
    public const int MaxKeyLength = 100;
}
