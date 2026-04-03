namespace Webhooks.Shared;

/// <summary>
/// Marks an action method as requiring webhook HMAC signature validation.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateWebhookSignatureAttribute : Attribute;

