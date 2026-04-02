using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Infrastructure;

public sealed class ChannelOutgoingWebhookQueue
    : BoundedChannelQueue<OutgoingWebhookItem>, IOutgoingWebhookQueue, IOutgoingWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelOutgoingWebhookQueue() : base(DefaultCapacity) { }
}
