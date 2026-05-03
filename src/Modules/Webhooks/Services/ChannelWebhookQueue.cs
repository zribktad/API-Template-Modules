using BuildingBlocks.Web.InfrastructureBackgroundJobs.Services;
using Webhooks.Contracts;

namespace Webhooks.Services;

public sealed class ChannelWebhookQueue
    : BoundedChannelQueue<WebhookPayload>,
        IWebhookProcessingQueue,
        IWebhookQueueReader
{
    private const int DefaultCapacity = 500;

    public ChannelWebhookQueue()
        : base(DefaultCapacity) { }
}
