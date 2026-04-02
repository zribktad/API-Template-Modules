using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Infrastructure.BackgroundJobs.Services;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// Bounded <see cref="System.Threading.Channels.Channel{T}"/> implementation for outgoing webhook dispatch,
/// exposing both producer (<see cref="IOutgoingWebhookQueue"/>) and consumer (<see cref="IOutgoingWebhookQueueReader"/>) interfaces.
/// </summary>
public sealed class ChannelOutgoingWebhookQueue
    : BoundedChannelQueue<OutgoingWebhookItem>,
        IOutgoingWebhookQueue,
        IOutgoingWebhookQueueReader
{
    private const int DefaultCapacity = 500;

    public ChannelOutgoingWebhookQueue()
        : base(DefaultCapacity) { }
}
