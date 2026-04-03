using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Core.Shared;

namespace Webhooks.Host.Shared;

public sealed class ChannelWebhookQueue
    : BoundedChannelQueue<WebhookPayload>, IWebhookProcessingQueue, IWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelWebhookQueue() : base(DefaultCapacity) { }
}
