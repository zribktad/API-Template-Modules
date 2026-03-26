using System.Net;
using System.Text.Json;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ExceptionHandling;

public class ApiExceptionHandlerTests
{
    private readonly Mock<ILogger<ApiExceptionHandler>> _loggerMock = new();
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var extensions = context.ProblemDetails.Extensions;
                var errorCode = extensions.TryGetValue("errorCode", out var code) && code is string existingCode
                    ? existingCode
                    : ErrorCatalog.General.Unknown;

                extensions["traceId"] = context.HttpContext.TraceIdentifier;
                extensions["errorCode"] = errorCode;
                context.ProblemDetails.Type ??= $"https://api-template.local/errors/{errorCode}";
            };
        });
        _problemDetailsService = services.BuildServiceProvider().GetRequiredService<IProblemDetailsService>();
    }

    public static IEnumerable<object[]> ExceptionMappingCases()
    {
        yield return
        [
            new NotFoundException("Product", Guid.Empty, ErrorCatalog.Reviews.ProductNotFoundForReview),
            HttpStatusCode.NotFound,
            "Not Found",
            $"Product with id '{Guid.Empty}' not found.",
            ErrorCatalog.Reviews.ProductNotFoundForReview
        ];
        yield return
        [
            new ValidationException("validation failed"),
            HttpStatusCode.BadRequest,
            "Bad Request",
            "validation failed",
            ErrorCatalog.General.ValidationFailed
        ];
        yield return
        [
            new InvalidOperationException("boom"),
            HttpStatusCode.InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            ErrorCatalog.General.Unknown
        ];
        yield return
        [
            new DbUpdateConcurrencyException("Concurrency conflict"),
            HttpStatusCode.Conflict,
            "Conflict",
            "The resource was modified by another request. Please retrieve the latest version and retry.",
            ErrorCatalog.General.ConcurrencyConflict
        ];
    }

    [Theory]
    [MemberData(nameof(ExceptionMappingCases))]
    public async Task TryHandleAsync_MapsExceptionToProblemDetails(
        Exception exception,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetail,
        string expectedErrorCode)
    {
        var context = CreateHttpContext();

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)expectedStatus);
        context.Response.ContentType.ShouldStartWith("application/problem+json");

        var body = await ReadJsonBody(context);
        body.GetProperty("status").GetInt32().ShouldBe((int)expectedStatus);
        body.GetProperty("title").GetString().ShouldBe(expectedTitle);
        body.GetProperty("detail").GetString().ShouldBe(expectedDetail);
        body.GetProperty("errorCode").GetString().ShouldBe(expectedErrorCode);
        body.GetProperty("type").GetString().ShouldBe($"https://api-template.local/errors/{expectedErrorCode}");
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_WhenGraphQlPath_ReturnsFalse()
    {
        var context = CreateHttpContext();
        context.Request.Path = "/graphql";

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WhenRequestIsAborted_ReturnsTrueWithoutProblemDetailsBody()
    {
        using var cts = new CancellationTokenSource();
        var context = CreateHttpContext();
        context.RequestAborted = cts.Token;
        cts.Cancel();

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(cts.Token), cts.Token);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(499);
        context.Response.Body.Length.ShouldBe(0);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadJsonBody(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        return json.RootElement.Clone();
    }
}
