using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Infrastructure.Health;

public sealed class WolverineHealthCheckOptions
{
    [Range(1, int.MaxValue)]
    public int DeadLetterWarningThreshold { get; init; } = 50;

    [Range(1, int.MaxValue)]
    public int DeadLetterCriticalThreshold { get; init; } = 200;

    [Range(1, int.MaxValue)]
    public int OutgoingBacklogWarningThreshold { get; init; } = 100;

    [Range(1, int.MaxValue)]
    public int IncomingBacklogWarningThreshold { get; init; } = 100;
}
