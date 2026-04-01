using Notifications.Application.Common.BackgroundJobs;
using SharedKernel.Application.BackgroundJobs;

namespace Notifications.Application.Common.Email;

/// <summary>
/// Write-side contract for enqueuing outbound email messages for asynchronous delivery.
/// </summary>
public interface IEmailQueue : IQueue<EmailMessage>;

/// <summary>
/// Read-side contract for the email background service to consume queued <see cref="EmailMessage"/> items.
/// </summary>
public interface IEmailQueueReader : IQueueReader<EmailMessage>;
