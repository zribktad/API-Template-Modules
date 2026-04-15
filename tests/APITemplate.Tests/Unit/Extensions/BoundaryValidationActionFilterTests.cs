using FluentValidation;
using Identity.Directory.Features.Role.CreateRole;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Contracts.Api.Filters;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Extensions;

public sealed class BoundaryValidationActionFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WhenValidatorFails_SetsProblemDetailsResult()
    {
        ServiceCollection services = new();
        services.AddSingleton<IValidator<CreateRoleRequest>, CreateRoleRequestValidator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        var filter = new BoundaryValidationActionFilter(provider);
        DefaultHttpContext httpContext = new() { RequestServices = provider };
        ActionContext actionContext = new(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary()
        );

        ActionExecutingContext context = new(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>
            {
                ["request"] = new CreateRoleRequest("", new List<string> { "Users.Read" }),
            },
            controller: null
        );

        bool nextCalled = false;

        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return Task.FromResult(
                    new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), null)
                );
            }
        );

        nextCalled.ShouldBeFalse();
        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        ProblemDetails problem = result.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Extensions["errorCode"].ShouldBe("GEN-0400");
    }
}
