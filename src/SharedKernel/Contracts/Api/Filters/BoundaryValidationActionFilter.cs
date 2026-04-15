using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedKernel.Application.Errors;
using SharedKernel.Contracts.Api;

namespace SharedKernel.Contracts.Api.Filters;

/// <summary>
///     Explicit HTTP-boundary validation that runs registered FluentValidation validators for bound action arguments.
///     This keeps request/filter models as plain data and keeps validator execution in one visible MVC layer.
/// </summary>
public sealed class BoundaryValidationActionFilter(IServiceProvider serviceProvider)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        if (!context.ModelState.IsValid)
        {
            await next();
            return;
        }

        List<Error> errors = [];

        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            Type validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            IEnumerable<object> validators = serviceProvider.GetServices(validatorType);

            foreach (IValidator validator in validators.Cast<IValidator>())
            {
                ValidationResult validationResult = await validator.ValidateAsync(
                    new ValidationContext<object>(argument),
                    context.HttpContext.RequestAborted
                );

                if (validationResult.IsValid)
                    continue;

                errors.AddRange(
                    validationResult.Errors.Select(e =>
                    {
                        Dictionary<string, object> metadata = [];
                        if (!string.IsNullOrWhiteSpace(e.PropertyName))
                            metadata["propertyName"] = e.PropertyName;
                        if (e.AttemptedValue is not null)
                            metadata["attemptedValue"] = e.AttemptedValue;

                        return Error.Validation(
                            ErrorCatalog.General.ValidationFailed,
                            e.ErrorMessage,
                            metadata.Count > 0 ? metadata : null
                        );
                    })
                );
            }
        }

        if (errors.Count == 0)
        {
            await next();
            return;
        }

        ProblemDetails problemDetails = errors.ToProblemDetails(context.HttpContext);
        context.Result = new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
