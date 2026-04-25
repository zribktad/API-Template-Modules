using Xunit;

namespace APITemplate.Tests.Integration;

[CollectionDefinition(IntegrationCollectionNames.HttpStateful)]
public sealed class HttpStatefulIntegrationTestsCollection
    : ICollectionFixture<CustomWebApplicationFactory> { }
