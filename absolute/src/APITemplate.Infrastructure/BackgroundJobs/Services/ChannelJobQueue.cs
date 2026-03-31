using APITemplate.Application.Common.BackgroundJobs;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Bounded in-process job queue backed by a <see cref="System.Threading.Channels.Channel{T}"/>.
/// Registered as a singleton and implements both <see cref="IJobQueue"/> (producer) and
/// <see cref="IJobQueueReader"/> (consumer) so that writers and readers stay decoupled.
/// </summary>
public sealed class ChannelJobQueue : BoundedChannelQueue<Guid>, IJobQueue, IJobQueueReader
{
    private const int DefaultCapacity = 100;

    public ChannelJobQueue()
        : base(DefaultCapacity) { }
}
