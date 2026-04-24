using Xunit;

namespace APITemplate.Tests.Integration;

public abstract class IntegrationCollectionTestBase
{
    protected CustomWebApplicationFactory Factory { get; }
    protected HttpClient Client => field ??= Factory.CreateClient();
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected IntegrationCollectionTestBase(CustomWebApplicationFactory factory) =>
        Factory = factory;
}
