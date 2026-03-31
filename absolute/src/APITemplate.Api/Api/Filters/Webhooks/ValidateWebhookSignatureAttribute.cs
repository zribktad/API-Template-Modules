namespace APITemplate.Api.Filters.Webhooks;

/// <summary>
/// Marks an action method as requiring webhook HMAC signature validation.
/// The actual enforcement is performed by <see cref="WebhookSignatureResourceFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateWebhookSignatureAttribute : Attribute;
