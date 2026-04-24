using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace APITemplate.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public abstract class IntegrationCollectionTestBase
{
    protected CustomWebApplicationFactory Factory { get; }
    protected HttpClient Client => field ??= Factory.CreateClient();
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected IntegrationCollectionTestBase(CustomWebApplicationFactory factory) =>
        Factory = factory;

    protected T GetService<T>()
        where T : notnull => Factory.Services.GetRequiredService<T>();

    protected IServiceScope CreateScope() => Factory.Services.CreateScope();
}
