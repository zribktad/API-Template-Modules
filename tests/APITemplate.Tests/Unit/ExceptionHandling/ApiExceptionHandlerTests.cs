using System.Net;
using System.Text.Json;
using APITemplate.Api.ExceptionHandling;
using Identity.Auth.Security;
using Identity.Directory.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Application.Errors;
using SharedKernel.Contracts.Api;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ExceptionHandling;

public class ApiExceptionHandlerTests
{
    private const string TestErrorTypeBaseUri = "https://unit-test.invalid/errors";

    private readonly Mock<ILogger<ApiExceptionHandler>> _loggerMock = new();
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandlerTests()
    {
        ServiceCollection services = new();
        services.AddOptions();
        services
            .AddOptions<ErrorDocumentationOptions>()
            .Configure(o => o.ErrorTypeBaseUri = TestErrorTypeBaseUri);
        services.AddProblemDetails();
        services.ConfigureOptions<ProblemDetailsErrorTypeConfigureOptions>();
        _problemDetailsService = services
            .BuildServiceProvider()
            .GetRequiredService<IProblemDetailsService>();
    }

    public static IEnumerable<object[]> ExceptionMappingCases()
    {
        yield return
        [
            new InvalidOperationException("boom"),
            HttpStatusCode.InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            ErrorCatalog.General.Unknown,
        ];
        yield return
        [
            new DbUpdateConcurrencyException("Concurrency conflict"),
            HttpStatusCode.Conflict,
            "Conflict",
            "The resource was modified by another request. Please retrieve the latest version and retry.",
            ErrorCatalog.General.ConcurrencyConflict,
        ];
        yield return
        [
            new UserAccessDeniedException(UserAccessErrorCodes.NoInvitation, "No invitation"),
            HttpStatusCode.InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            UserAccessErrorCodes.NoInvitation,
        ];
    }

    [Theory]
    [MemberData(nameof(ExceptionMappingCases))]
    public async Task TryHandleAsync_MapsExceptionToProblemDetails(
        Exception exception,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetail,
        string expectedErrorCode
    )
    {
        DefaultHttpContext context = CreateHttpContext();

        ApiExceptionHandler handler = new(_loggerMock.Object, _problemDetailsService);
        bool handled = await handler.TryHandleAsync(
            context,
            exception,
            TestContext.Current.CancellationToken
        );

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)expectedStatus);
        context.Response.ContentType.ShouldStartWith("application/problem+json");

        JsonElement body = await ReadJsonBody(context);
        body.GetProperty("status").GetInt32().ShouldBe((int)expectedStatus);
        body.GetProperty("title").GetString().ShouldBe(expectedTitle);
        body.GetProperty("detail").GetString().ShouldBe(expectedDetail);
        body.GetProperty("errorCode").GetString().ShouldBe(expectedErrorCode);
        string? type = body.GetProperty("type").GetString();
        type.ShouldBe(
            ProblemDetailsErrorTypeUri.BuildAbsoluteUri(TestErrorTypeBaseUri, expectedErrorCode)
        );
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_WhenGraphQlPath_ReturnsFalse()
    {
        DefaultHttpContext context = CreateHttpContext();
        context.Request.Path = "/graphql";

        ApiExceptionHandler handler = new(_loggerMock.Object, _problemDetailsService);
        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken
        );

        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WhenRequestIsAborted_ReturnsTrueWithoutProblemDetailsBody()
    {
        using CancellationTokenSource cts = new();
        DefaultHttpContext context = CreateHttpContext();
        context.RequestAborted = cts.Token;
        cts.Cancel();

        ApiExceptionHandler handler = new(_loggerMock.Object, _problemDetailsService);
        bool handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(cts.Token),
            cts.Token
        );

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(499);
        context.Response.Body.Length.ShouldBe(0);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/api/v1/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadJsonBody(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        JsonDocument json = await JsonDocument.ParseAsync(context.Response.Body);
        return json.RootElement.Clone();
    }
}
