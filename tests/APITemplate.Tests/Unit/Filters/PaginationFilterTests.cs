using BuildingBlocks.Domain.Common;
using BuildingBlocks.Web.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
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
        DefaultHttpContext httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.test.com");
        httpContext.Request.Path = "/api/items";
        httpContext.Request.QueryString = new QueryString("?pageSize=10");

        PagedResponse<string> pagedResponse = new PagedResponse<string>(["Item1"], 20, 2, 10); // total 20, page 2, pageSize 10

        ActionContext actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor()
        );

        ResultExecutingContext resultExecutingContext = new ResultExecutingContext(
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
        IHeaderDictionary responseHeaders = httpContext.Response.Headers;

        responseHeaders.ShouldContainKey("Link");
        string linkHeader = responseHeaders["Link"].ToString();
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
        DefaultHttpContext httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.test.com");
        httpContext.Request.Path = "/api/items";

        PagedResponse<string> pagedResponse = new PagedResponse<string>(["Item1"], 20, 1, 10); // total 20, page 1, pageSize 10
        IResult valueHttpResult = TypedResults.Ok(pagedResponse);

        DefaultEndpointFilterInvocationContext endpointFilterInvocationContext =
            new DefaultEndpointFilterInvocationContext(httpContext);

        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(valueHttpResult);

        // Act
        object? result = await _sut.InvokeAsync(endpointFilterInvocationContext, next);

        // Assert
        result.ShouldBeSameAs(valueHttpResult);

        IHeaderDictionary responseHeaders = httpContext.Response.Headers;
        responseHeaders.ShouldContainKey("Link");
        string linkHeader = responseHeaders["Link"].ToString();
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
        DefaultHttpContext httpContext = new DefaultHttpContext();
        ActionContext actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor()
        );

        ResultExecutingContext resultExecutingContext = new ResultExecutingContext(
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
