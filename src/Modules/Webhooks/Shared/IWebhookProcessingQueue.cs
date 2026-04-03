using SharedKernel.Application.BackgroundJobs;
namespace Webhooks.Shared;

/// <summary>
/// Write-side contract for enqueuing inbound webhook payloads awaiting processing.
/// </summary>
public interface IWebhookProcessingQueue : IQueue<WebhookPayload>;

/// <summary>
/// Read-side contract for consuming inbound webhook payloads from the processing queue.
/// </summary>
public interface IWebhookQueueReader : IQueueReader<WebhookPayload>;

