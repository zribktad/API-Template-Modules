namespace Webhooks.Entities;

public class IncomingWebhook
{
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; set; }
}
