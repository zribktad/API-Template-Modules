using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Observability;

public sealed class HttpRouteResolverTests
{
    [Fact]
    public void ReplaceVersionToken_WhenRouteContainsApiVersionConstraint_ReplacesWithConcreteVersion()
    {
        var resolvedRoute = HttpRouteResolver.ReplaceVersionToken(
            "api/v{version:apiVersion}/Products",
            new RouteValueDictionary { ["version"] = "1" }
        );

        resolvedRoute.ShouldBe("api/v1/Products");
    }

    [Fact]
    public void ReplaceVersionToken_WhenVersionMissing_LeavesTemplateUnchanged()
    {
        var resolvedRoute = HttpRouteResolver.ReplaceVersionToken(
            "api/v{version:apiVersion}/Products",
            new RouteValueDictionary()
        );

        resolvedRoute.ShouldBe("api/v{version:apiVersion}/Products");
    }

    [Fact]
    public void Resolve_WhenEndpointTemplateMissing_FallsBackToRequestPath()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/Products";

        var resolvedRoute = HttpRouteResolver.Resolve(httpContext);

        resolvedRoute.ShouldBe("/api/v1/Products");
    }
}
