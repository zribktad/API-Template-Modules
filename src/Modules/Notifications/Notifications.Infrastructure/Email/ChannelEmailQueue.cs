using Notifications.Application.Common.Email;
using Notifications.Infrastructure.BackgroundJobs.Services;

namespace Notifications.Infrastructure.Email;

/// <summary>
/// Bounded in-process email queue backed by a <see cref="System.Threading.Channels.Channel{T}"/>.
/// Implements both <see cref="IEmailQueue"/> (producer) and <see cref="IEmailQueueReader"/> (consumer)
/// so that callers and the sending background service remain decoupled.
/// </summary>
public sealed class ChannelEmailQueue
    : SharedKernel.Infrastructure.BackgroundJobs.Services.BoundedChannelQueue<EmailMessage>,
        IEmailQueue,
        IEmailQueueReader
{
    private const int DefaultCapacity = 1000;

    public ChannelEmailQueue()
        : base(DefaultCapacity) { }
}
