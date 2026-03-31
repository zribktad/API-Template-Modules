using APITemplate.Application.Features.Examples.DTOs;

namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Write-side contract for enqueuing outgoing webhook dispatch items.
/// </summary>
public interface IOutgoingWebhookQueue : IQueue<OutgoingWebhookItem>;

/// <summary>
/// Read-side contract for consuming outgoing webhook items from the queue.
/// </summary>
public interface IOutgoingWebhookQueueReader : IQueueReader<OutgoingWebhookItem>;
