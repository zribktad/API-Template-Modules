using APITemplate.Api.Extensions;
using APITemplate.Application.Common.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Observability;

public sealed class ObservabilityServiceCollectionExtensionsTests
{
    [Fact]
    public void GetEnabledOtlpEndpoints_WhenDevelopmentOutsideContainer_DefaultsToAspire()
    {
        var options = new ObservabilityOptions();

        var endpoints = ObservabilityServiceCollectionExtensions.GetEnabledOtlpEndpoints(
            options,
            new FakeHostEnvironment(Environments.Development)
        );

        endpoints.ShouldContain("http://localhost:4317");
    }

    [Fact]
    public void GetEnabledOtlpEndpoints_WhenExplicitOtlpEnabled_IncludesConfiguredEndpoint()
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

        var endpoints = ObservabilityServiceCollectionExtensions.GetEnabledOtlpEndpoints(
            options,
            new FakeHostEnvironment(Environments.Production)
        );

        endpoints.ShouldBe(["http://alloy:4317"]);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "APITemplate.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
