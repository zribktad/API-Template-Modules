using SharedKernel.Application.Options.Infrastructure;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Observability;

public sealed class ObservabilityOptionsResolveOtlpEndpointTests
{
    [Fact]
    public void ResolveOtlpEndpoint_WhenDevelopment_DefaultsToAspireEndpoint()
    {
        var options = new ObservabilityOptions
        {
            Aspire = new AspireEndpointOptions { Endpoint = "http://localhost:4317" },
        };

        Uri? result = options.ResolveOtlpEndpoint(isDevelopment: true);

        result.ShouldNotBeNull();
        result.AbsoluteUri.ShouldBe("http://localhost:4317/");
    }

    [Fact]
    public void ResolveOtlpEndpoint_WhenProduction_WithNoExplicitToggle_ReturnsNull()
    {
        var options = new ObservabilityOptions
        {
            Aspire = new AspireEndpointOptions { Endpoint = "http://localhost:4317" },
        };

        Uri? result = options.ResolveOtlpEndpoint(isDevelopment: false);

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveOtlpEndpoint_WhenExplicitOtlpEnabled_ReturnsOtlpEndpoint()
    {
        var options = new ObservabilityOptions
        {
            Otlp = new OtlpEndpointOptions { Endpoint = "http://alloy:4317" },
            Exporters = new ObservabilityExportersOptions
            {
                Otlp = new ObservabilityExporterToggleOptions { Enabled = true },
                Aspire = new ObservabilityExporterToggleOptions { Enabled = false },
            },
        };

        Uri? result = options.ResolveOtlpEndpoint(isDevelopment: false);

        result.ShouldNotBeNull();
        result.AbsoluteUri.ShouldBe("http://alloy:4317/");
    }

    [Fact]
    public void ResolveOtlpEndpoint_WhenOtlpEnabled_PrefersOtlpOverAspire()
    {
        var options = new ObservabilityOptions
        {
            Otlp = new OtlpEndpointOptions { Endpoint = "http://alloy:4317" },
            Aspire = new AspireEndpointOptions { Endpoint = "http://localhost:4317" },
            Exporters = new ObservabilityExportersOptions
            {
                Otlp = new ObservabilityExporterToggleOptions { Enabled = true },
                Aspire = new ObservabilityExporterToggleOptions { Enabled = true },
            },
        };

        Uri? result = options.ResolveOtlpEndpoint(isDevelopment: true);

        result.ShouldNotBeNull();
        result.AbsoluteUri.ShouldBe("http://alloy:4317/");
    }

    [Fact]
    public void ResolveOtlpEndpoint_WhenOtlpEnabled_WithInvalidUri_FallsBackToAspire()
    {
        var options = new ObservabilityOptions
        {
            Otlp = new OtlpEndpointOptions { Endpoint = "not-a-uri" },
            Aspire = new AspireEndpointOptions { Endpoint = "http://localhost:4317" },
            Exporters = new ObservabilityExportersOptions
            {
                Otlp = new ObservabilityExporterToggleOptions { Enabled = true },
                Aspire = new ObservabilityExporterToggleOptions { Enabled = true },
            },
        };

        Uri? result = options.ResolveOtlpEndpoint(isDevelopment: false);

        result.ShouldNotBeNull();
        result.AbsoluteUri.ShouldBe("http://localhost:4317/");
    }
}
