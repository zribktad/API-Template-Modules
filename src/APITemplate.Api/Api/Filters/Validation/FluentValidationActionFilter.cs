using APITemplate.Infrastructure.Observability;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace APITemplate.Api.Filters.Validation;

/// <summary>
/// Global action filter that automatically validates all action arguments using FluentValidation.
/// Runs before every controller action. If a registered <see cref="IValidator{T}"/> exists for an
/// argument type, it is resolved from DI and executed. On failure, returns HTTP 400 with a
/// <see cref="ValidationProblemDetails"/> body — the controller method is never invoked.
/// Arguments without a registered validator are silently skipped.
/// </summary>
public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public FluentValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Iterates over all action arguments, resolves a matching <see cref="IValidator{T}"/> from DI
    /// for each, and short-circuits with HTTP 400 if any validation fails.
    /// </summary>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = _serviceProvider.GetService(validatorType) as IValidator;

            if (validator is null)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted
            );

            if (result.IsValid)
                continue;

            ValidationTelemetry.RecordValidationFailure(context, argumentType, result.Errors);
            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(
                new ValidationProblemDetails(context.ModelState)
            );
            return;
        }

        await next();
    }
}
