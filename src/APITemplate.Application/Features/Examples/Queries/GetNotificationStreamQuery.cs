using System.Runtime.CompilerServices;
using APITemplate.Application.Features.Examples.DTOs;

namespace APITemplate.Application.Features.Examples;

public sealed record GetNotificationStreamQuery(SseStreamRequest Request);

public sealed class GetNotificationStreamQueryHandler
{
    public static Task<IAsyncEnumerable<SseNotificationItem>> HandleAsync(
        GetNotificationStreamQuery request,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        return Task.FromResult(StreamNotifications(request.Request.Count, timeProvider, ct));
    }

    private static async IAsyncEnumerable<SseNotificationItem> StreamNotifications(
        int count,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        for (var i = 1; i <= count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            yield return new SseNotificationItem(
                i,
                $"Event {i} of {count}",
                timeProvider.GetUtcNow().UtcDateTime
            );
        }
    }
}
