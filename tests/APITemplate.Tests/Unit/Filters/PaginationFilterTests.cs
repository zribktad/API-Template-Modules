using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using SharedKernel.Contracts.Api.Filters;
using SharedKernel.Domain.Common;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Filters;

[Trait("Category", "Unit")]
public class PaginationFilterTests
{
    private readonly PaginationFilter _sut;

    public PaginationFilterTests()
    {
        _sut = new PaginationFilter();
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenResultIsPagedResponse_AddsLinkHeaders()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.test.com");
        httpContext.Request.Path = "/api/items";
        httpContext.Request.QueryString = new QueryString("?limit=10");

        var pagedResponse = new PagedResponse<string>(["Item1"], 20, 2, 10); // total 20, page 2, limit 10

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(pagedResponse),
            new object()
        );

        ResultExecutionDelegate next = () =>
            Task.FromResult(
                new ResultExecutedContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    resultExecutingContext.Result,
                    new object()
                )
            );

        // Act
        await _sut.OnResultExecutionAsync(resultExecutingContext, next);

        // Assert
        var responseHeaders = httpContext.Response.Headers;

        responseHeaders.ShouldContainKey("Link");
        var linkHeader = responseHeaders["Link"].ToString();
        linkHeader.ShouldContain("rel=\"first\"");
        linkHeader.ShouldContain("rel=\"last\"");
        linkHeader.ShouldContain("rel=\"prev\"");
        linkHeader.ShouldNotContain("rel=\"next\""); // Page 2 of 2 (20/10), so no next

        responseHeaders.ShouldContainKey("X-Pagination-Total-Count");
        responseHeaders["X-Pagination-Total-Count"].ToString().ShouldBe("20");

        responseHeaders.ShouldContainKey("Access-Control-Expose-Headers");
    }

    [Fact]
    public async Task InvokeAsync_WhenMinimalApiResultIsPagedResponse_AddsLinkHeaders()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.test.com");
        httpContext.Request.Path = "/api/items";

        var pagedResponse = new PagedResponse<string>(["Item1"], 20, 1, 10); // total 20, page 1, limit 10
        var valueHttpResult = TypedResults.Ok(pagedResponse);

        var endpointFilterInvocationContext = new DefaultEndpointFilterInvocationContext(
            httpContext
        );

        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(valueHttpResult);

        // Act
        var result = await _sut.InvokeAsync(endpointFilterInvocationContext, next);

        // Assert
        result.ShouldBeSameAs(valueHttpResult);

        var responseHeaders = httpContext.Response.Headers;
        responseHeaders.ShouldContainKey("Link");
        var linkHeader = responseHeaders["Link"].ToString();
        linkHeader.ShouldContain("rel=\"first\"");
        linkHeader.ShouldContain("rel=\"last\"");
        linkHeader.ShouldContain("rel=\"next\"");
        linkHeader.ShouldNotContain("rel=\"prev\""); // Page 1, so no prev

        responseHeaders.ShouldContainKey("X-Pagination-Total-Count");
        responseHeaders["X-Pagination-Total-Count"].ToString().ShouldBe("20");
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenResultIsNotPagedResponse_DoesNothing()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult("Not a paged response"),
            new object()
        );

        ResultExecutionDelegate next = () =>
            Task.FromResult(
                new ResultExecutedContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    resultExecutingContext.Result,
                    new object()
                )
            );

        // Act
        await _sut.OnResultExecutionAsync(resultExecutingContext, next);

        // Assert
        httpContext.Response.Headers.ShouldNotContainKey("Link");
    }
}
