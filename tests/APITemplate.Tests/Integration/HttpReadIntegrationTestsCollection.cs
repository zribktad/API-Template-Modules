using Xunit;

namespace APITemplate.Tests.Integration;

public static class IntegrationCollectionNames
{
    public const string HttpRead = "HttpReadIntegration";
    public const string HttpStateful = "HttpStatefulIntegration";
}

[CollectionDefinition(IntegrationCollectionNames.HttpRead)]
public sealed class HttpReadIntegrationTestsCollection
    : ICollectionFixture<CustomWebApplicationFactory> { }
