using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace APITemplate.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public abstract class IntegrationTestBase<TFactory> : IClassFixture<TFactory>
    where TFactory : CustomWebApplicationFactory
{
    protected TFactory Factory { get; }
    protected HttpClient Client =>
        field ??= Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected IntegrationTestBase(TFactory factory) => Factory = factory;

    protected T GetService<T>()
        where T : notnull => Factory.Services.GetRequiredService<T>();

    protected IServiceScope CreateScope() => Factory.Services.CreateScope();
}
