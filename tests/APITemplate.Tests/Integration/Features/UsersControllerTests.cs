using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Common;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public class UsersControllerTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _tenant = default!;
    private AppUser _user = default!;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_tenant, _user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "users_test",
            "users_test@test.com",
            ct: ct
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AuthenticateAsAdmin() =>
        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _user.Id,
            tenantId: _tenant.Id,
            username: _user.Username.Value,
            role: "PlatformAdmin",
            permissions: [Permission.Users.Read],
            email: _user.Email.Value,
            subject: _user.KeycloakUserId
        );

    private void AuthenticateWithPermissions(params string[] permissions) =>
        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _user.Id,
            tenantId: _tenant.Id,
            username: _user.Username.Value,
            role: "PlatformAdmin",
            permissions: permissions,
            email: _user.Email.Value,
            subject: _user.KeycloakUserId
        );

    [Fact]
    public async Task GetMe_WithAuthenticatedNonAdminUser_ReturnsCurrentUser()
    {
        var ct = TestContext.Current.CancellationToken;

        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _user.Id,
            tenantId: _tenant.Id,
            username: _user.Username.Value,
            role: "User",
            email: _user.Email.Value,
            subject: _user.KeycloakUserId
        );

        var response = await _client.GetAsync("/api/v1/users/me", ct);
        var payload = await response.Content.ReadFromJsonAsync<UserResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.ShouldNotBeNull();
        payload!.Id.ShouldBe(_user.Id);
        payload.Username.ShouldBe(_user.Username.Value);
        payload.Email.ShouldBe(_user.Email.Value);
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedNonAdminUser_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;

        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _user.Id,
            tenantId: _tenant.Id,
            username: _user.Username.Value,
            role: "User",
            email: _user.Email.Value,
            subject: _user.KeycloakUserId
        );

        var response = await _client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_FilterByPartialUsername_ReturnsMatchingUser()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a user with a unique username for this test
        string uniqueUsername = $"filtertest{_user.Id:N}"[..20];
        AppUser seededUser = await IntegrationAuthHelper.SeedUserInTenantAsync(
            _factory.Services,
            _tenant.Id,
            uniqueUsername,
            $"{uniqueUsername}@test.com",
            ct: ct
        );

        AuthenticateAsAdmin();

        // Search by partial (first 8 chars)
        string partial = uniqueUsername[..8];
        var response = await _client.GetAsync($"/api/v1/users?username={partial}&pageSize=100", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<UserResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        payload!.Items.ShouldContain(u => u.Id == seededUser.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsConflictProblemDetailsWithErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateWithPermissions(Permission.Users.Create);

        var request = new CreateUserRequest("new-user-duplicate", _user.Email.Value);
        var response = await _client.PostAsJsonAsync("/api/v1/users", request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(
                TestJsonOptions.CaseInsensitive,
                ct
            );
        problem.ShouldNotBeNull();
        ExtractErrorCode(problem!).ShouldBe("USR-0409-EMAIL");
    }

    private static string ExtractErrorCode(HttpValidationProblemDetails problem)
    {
        return
            problem.Extensions.TryGetValue("errorCode", out object? code) && code is JsonElement je
            ? je.GetString() ?? string.Empty
            : code?.ToString() ?? string.Empty;
    }
}
