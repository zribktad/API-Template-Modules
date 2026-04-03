using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Contracts;
using Webhooks.Services;
using Webhooks.Security;

namespace Webhooks.Services;

public sealed class ChannelWebhookQueue
    : BoundedChannelQueue<WebhookPayload>, IWebhookProcessingQueue, IWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelWebhookQueue() : base(DefaultCapacity) { }
}




