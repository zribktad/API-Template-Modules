using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Shared;

namespace Webhooks.Shared;

public sealed class ChannelWebhookQueue
    : BoundedChannelQueue<WebhookPayload>, IWebhookProcessingQueue, IWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelWebhookQueue() : base(DefaultCapacity) { }
}

