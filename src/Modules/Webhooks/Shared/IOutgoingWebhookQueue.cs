using SharedKernel.Application.BackgroundJobs;
namespace Webhooks.Shared;

/// <summary>
/// Write-side contract for enqueuing outgoing webhook dispatch items.
/// </summary>
public interface IOutgoingWebhookQueue : IQueue<OutgoingWebhookItem>;

/// <summary>
/// Read-side contract for consuming outgoing webhook items from the queue.
/// </summary>
public interface IOutgoingWebhookQueueReader : IQueueReader<OutgoingWebhookItem>;

