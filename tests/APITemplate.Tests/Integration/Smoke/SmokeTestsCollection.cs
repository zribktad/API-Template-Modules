using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[CollectionDefinition("Smoke")]
public sealed class SmokeTestsCollection : ICollectionFixture<CustomWebApplicationFactory> { }
