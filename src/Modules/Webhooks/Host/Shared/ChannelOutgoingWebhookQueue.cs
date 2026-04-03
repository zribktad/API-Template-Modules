using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Core.Shared;

namespace Webhooks.Host.Shared;

public sealed class ChannelOutgoingWebhookQueue
    : BoundedChannelQueue<OutgoingWebhookItem>, IOutgoingWebhookQueue, IOutgoingWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelOutgoingWebhookQueue() : base(DefaultCapacity) { }
}
