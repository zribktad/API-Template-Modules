using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Infrastructure.BackgroundJobs.Services;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// Bounded <see cref="System.Threading.Channels.Channel{T}"/> implementation for incoming webhook processing,
/// exposing both producer (<see cref="IWebhookProcessingQueue"/>) and consumer (<see cref="IWebhookQueueReader"/>) interfaces.
/// </summary>
public sealed class ChannelWebhookQueue
    : BoundedChannelQueue<WebhookPayload>,
        IWebhookProcessingQueue,
        IWebhookQueueReader
{
    private const int DefaultCapacity = 500;

    public ChannelWebhookQueue()
        : base(DefaultCapacity) { }
}
