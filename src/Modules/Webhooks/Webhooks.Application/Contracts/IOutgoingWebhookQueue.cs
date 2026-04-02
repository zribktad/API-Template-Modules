using SharedKernel.Application.BackgroundJobs;
using Webhooks.Application.DTOs;

namespace Webhooks.Application.Contracts;

/// <summary>
/// Write-side contract for enqueuing outgoing webhook dispatch items.
/// </summary>
public interface IOutgoingWebhookQueue : IQueue<OutgoingWebhookItem>;

/// <summary>
/// Read-side contract for consuming outgoing webhook items from the queue.
/// </summary>
public interface IOutgoingWebhookQueueReader : IQueueReader<OutgoingWebhookItem>;
