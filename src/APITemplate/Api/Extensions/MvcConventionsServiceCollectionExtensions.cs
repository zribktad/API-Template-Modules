using BuildingBlocks.Application.Http;
using BuildingBlocks.Application.Validation;
using BuildingBlocks.Web.Api;
using BuildingBlocks.Web.Api.Routing;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring MVC conventions and behaviors.
/// </summary>
public static class MvcConventionsServiceCollectionExtensions
{
    /// <summary>
    ///     Applies kebab-case URL routing and maps model validation errors to RFC 7807 ProblemDetails.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddMvcConventions(this IServiceCollection services)
    {
        services.Configure<MvcOptions>(options =>
        {
            options.Conventions.Add(
                new RouteTokenTransformerConvention(new KebabCaseRouteTokenTransformer())
            );
            options.Filters.Add<BuildingBlocks.Web.Api.Filters.PaginationFilter>();
            options.Filters.Add<BuildingBlocks.Web.Api.Filters.Idempotency.IdempotencyActionFilter>();
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = BuildModelStateErrorResponse;
        });

        return services;
    }

    /// <summary>
    ///     Builds a standardized ProblemDetails response for invalid model state.
    /// </summary>
    private static IActionResult BuildModelStateErrorResponse(ActionContext context)
    {
        List<Error> errors = context
            .ModelState.Where(kvp => kvp.Value is { Errors.Count: > 0 })
            .SelectMany(kvp =>
                kvp.Value!.Errors.Select(error =>
                {
                    Dictionary<string, object> metadata = [];
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                        metadata[ValidationConstants.PropertyNameKey] = kvp.Key;

                    return Error.Validation(
                        ErrorCatalog.General.ValidationFailed,
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The request is invalid."
                            : error.ErrorMessage,
                        metadata.Count > 0 ? metadata : null
                    );
                })
            )
            .ToList();

        if (errors.Count == 0)
        {
            errors.Add(
                Error.Validation(ErrorCatalog.General.ValidationFailed, "The request is invalid.")
            );
        }

        ProblemDetails problemDetails = errors.ToProblemDetails(context.HttpContext);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
