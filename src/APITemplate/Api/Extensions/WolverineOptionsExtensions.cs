using Wolverine;
using Wolverine.ErrorHandling;

namespace APITemplate.Api.Extensions;

public static class WolverineOptionsExtensions
{
    public static void AddDurableRetryPolicy<TException>(this WolverineOptions options)
        where TException : Exception
    {
        options
            .OnException<TException>()
            .ScheduleRetry(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(5)
            )
            .Then.MoveToErrorQueue();
    }
}
