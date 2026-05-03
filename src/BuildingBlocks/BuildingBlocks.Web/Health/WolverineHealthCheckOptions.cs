using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Web.Health;

public sealed class WolverineHealthCheckOptions : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int DeadLetterWarningThreshold { get; init; } = 50;

    [Range(1, int.MaxValue)]
    public int DeadLetterCriticalThreshold { get; init; } = 200;

    [Range(1, int.MaxValue)]
    public int OutgoingBacklogWarningThreshold { get; init; } = 100;

    [Range(1, int.MaxValue)]
    public int IncomingBacklogWarningThreshold { get; init; } = 100;

    //Used by ValidateOnStart
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeadLetterWarningThreshold >= DeadLetterCriticalThreshold)
        {
            yield return new ValidationResult(
                "DeadLetterWarningThreshold must be less than DeadLetterCriticalThreshold",
                [nameof(DeadLetterWarningThreshold), nameof(DeadLetterCriticalThreshold)]
            );
        }
    }
}

