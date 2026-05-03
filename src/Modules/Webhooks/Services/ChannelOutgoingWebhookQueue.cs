using BuildingBlocks.Web.InfrastructureBackgroundJobs.Services;
using Webhooks.Contracts;

namespace Webhooks.Services;

public sealed class ChannelOutgoingWebhookQueue
    : BoundedChannelQueue<OutgoingWebhookItem>,
        IOutgoingWebhookQueue,
        IOutgoingWebhookQueueReader
{
    private const int DefaultCapacity = 500;

    public ChannelOutgoingWebhookQueue()
        : base(DefaultCapacity) { }
}
