using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SharedKernel.Infrastructure.Health;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Health;

[Trait("Category", "Unit")]
public sealed class OtlpCollectorHealthCheckTests
{
    private const string TestEndpoint = "http://localhost:4318";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

    private OtlpCollectorHealthCheck CreateSut()
    {
        HttpClient httpClient = new(_httpMessageHandler.Object);
        _httpClientFactory
            .Setup(f => f.CreateClient(nameof(OtlpCollectorHealthCheck)))
            .Returns(httpClient);

        OtlpCollectorHealthCheckOptions options = new() { Endpoint = TestEndpoint };
        return new OtlpCollectorHealthCheck(_httpClientFactory.Object, Options.Create(options));
    }

    [Fact]
    public async Task Returns_Healthy_When_Endpoint_Returns_Success()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        OtlpCollectorHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Returns_Unhealthy_When_Endpoint_Returns_Non_Success_Status()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        OtlpCollectorHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("503");
    }

    [Fact]
    public async Task Returns_Unhealthy_When_Endpoint_Is_Not_Reachable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        OtlpCollectorHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not reachable");
    }
}
