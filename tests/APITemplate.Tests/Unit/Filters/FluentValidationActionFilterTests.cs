using APITemplate.Api.Filters.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Filters;

public sealed class FluentValidationActionFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WhenValidationFails_ReturnsBadRequest()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<TestRequest>, TestRequestValidator>();
        var sut = new FluentValidationActionFilter(services.BuildServiceProvider());

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Request.Path = "/api/v1/test";

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor
            {
                AttributeRouteInfo = new AttributeRouteInfo { Template = "api/v1/test" },
            }
        );

        var context = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?> { ["request"] = new TestRequest(string.Empty) },
            controller: new object()
        );

        await sut.OnActionExecutionAsync(
            context,
            () => throw new InvalidOperationException("Should not execute")
        );

        context.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    private sealed record TestRequest(string Name);

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
