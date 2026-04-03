using System.Net;
using System.Net.Http.Json;
using APITemplate.Domain.Enums;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMe_WithAuthenticatedNonAdminUser_ReturnsCurrentUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tenant, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username: "regular-user",
            email: "regular-user@example.com",
            ct: ct
        );

        IntegrationAuthHelper.Authenticate(
            _client,
            user.Id,
            tenant.Id,
            user.Username,
            UserRole.User
        );

        var response = await _client.GetAsync("/api/v1/users/me", ct);
        var payload = await response.Content.ReadFromJsonAsync<UserResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.ShouldNotBeNull();
        payload!.Id.ShouldBe(user.Id);
        payload.Username.ShouldBe(user.Username);
        payload.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedNonAdminUser_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, role: UserRole.User);

        var response = await _client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
