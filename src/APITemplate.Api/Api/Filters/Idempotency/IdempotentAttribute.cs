namespace APITemplate.Api.Filters.Idempotency;

/// <summary>
/// Marks an action method as idempotent, enabling the <see cref="IdempotencyActionFilter"/>
/// to store and replay responses using the <c>Idempotency-Key</c> request header.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute
{
    public int TtlHours { get; set; } = IdempotencyConstants.DefaultTtlHours;
    public int LockTimeoutSeconds { get; set; } = IdempotencyConstants.LockTimeoutSeconds;
}
