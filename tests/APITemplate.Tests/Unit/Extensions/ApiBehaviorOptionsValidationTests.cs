using APITemplate.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Extensions;

public sealed class ApiBehaviorOptionsValidationTests
{
    [Fact]
    public void InvalidModelStateResponseFactory_MapsBoundaryValidationToProblemDetails()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ErrorDocumentation:ErrorTypeBaseUri"] = "https://api-template.local/errors/",
                }
            )
            .Build();

        services.AddApiFoundation(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        ApiBehaviorOptions options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        ModelStateDictionary modelState = new();
        modelState.AddModelError("Rating", "Rating must be between 1 and 5.");

        DefaultHttpContext httpContext = new() { RequestServices = provider };
        ActionContext actionContext = new(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            modelState
        );

        ObjectResult result = options
            .InvalidModelStateResponseFactory(actionContext)
            .ShouldBeOfType<ObjectResult>();
        ProblemDetails problem = result.Value.ShouldBeOfType<ProblemDetails>();

        result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Title.ShouldBe("Bad Request");
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("Rating must be between 1 and 5.");
        problem.Extensions["errorCode"].ShouldBe("GEN-0400");
        ((Dictionary<string, object>)problem.Extensions["metadata"]!).ShouldContainKey("propertyName");
    }
}
