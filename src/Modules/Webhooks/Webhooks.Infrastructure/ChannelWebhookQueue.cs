using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Infrastructure;

public sealed class ChannelWebhookQueue
    : BoundedChannelQueue<WebhookPayload>, IWebhookProcessingQueue, IWebhookQueueReader
{
    private const int DefaultCapacity = 500;
    public ChannelWebhookQueue() : base(DefaultCapacity) { }
}
