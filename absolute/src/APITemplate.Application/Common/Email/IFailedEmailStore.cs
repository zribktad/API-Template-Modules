namespace APITemplate.Application.Common.Email;

/// <summary>
/// Application-layer abstraction for persisting emails that could not be delivered,
/// enabling later inspection, manual retry, or dead-letter analysis.
/// </summary>
public interface IFailedEmailStore
{
    /// <summary>
    /// Persists <paramref name="message"/> along with the <paramref name="error"/> description
    /// so it can be reviewed or retried by the email retry background job.
    /// </summary>
    Task StoreFailedAsync(EmailMessage message, string error, CancellationToken ct = default);
}
