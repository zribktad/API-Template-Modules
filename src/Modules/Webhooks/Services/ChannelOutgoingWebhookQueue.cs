using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Contracts;
using Webhooks.Services;
using Webhooks.Security;

namespace Webhooks.Services;

public sealed class ChannelOutgoingWebhookQueue
    : BoundedChannelQueue<OutgoingWebhookItem>, IOutgoingWebhookQueue, IOutgoingWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelOutgoingWebhookQueue() : base(DefaultCapacity) { }
}




