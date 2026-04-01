using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace APITemplate.Api.Filters.Validation;

/// <summary>
/// Global action filter that resolves FluentValidation validators from DI for each action argument
/// and short-circuits with HTTP 400 if validation fails.
/// </summary>
public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypeCache = new();

    private readonly IServiceProvider _serviceProvider;

    public FluentValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            Type argumentType = argument.GetType();
            Type validatorType = ValidatorTypeCache.GetOrAdd(
                argumentType,
                static t => typeof(IValidator<>).MakeGenericType(t)
            );
            IValidator? validator = _serviceProvider.GetService(validatorType) as IValidator;

            if (validator is null)
                continue;

            ValidationContext<object> validationContext = new(argument);
            FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted
            );

            if (result.IsValid)
                continue;

            foreach (FluentValidation.Results.ValidationFailure error in result.Errors)
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
