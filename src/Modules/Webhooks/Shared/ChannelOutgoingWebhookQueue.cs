using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Shared;

namespace Webhooks.Shared;

public sealed class ChannelOutgoingWebhookQueue
    : BoundedChannelQueue<OutgoingWebhookItem>, IOutgoingWebhookQueue, IOutgoingWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelOutgoingWebhookQueue() : base(DefaultCapacity) { }
}

