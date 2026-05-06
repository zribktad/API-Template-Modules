using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Domain.Interfaces;
using Identity.Auth.Features.Bff.DTOs;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.Directory.Interfaces;
using Identity.Persistence;
using Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Identity;

[Trait("Category", "Integration.Ldap")]
public sealed class LdapIntegrationTests : IntegrationTestBase<CustomWebApplicationFactory>
{
    public LdapIntegrationTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    private async Task SeedBootstrapTenantAsync()
    {
        using IServiceScope scope = CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<AuthBootstrapSeeder>();
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BootstrapTenant_ShouldExist()
    {
        await SeedBootstrapTenantAsync();
        using IServiceScope scope = CreateScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Guid bootstrapId = Guid.Parse(AuthConstants.Tenants.Bootstrap);
        var tenant = await dbContext
            .Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == bootstrapId, TestContext.Current.CancellationToken);
        tenant.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetUserAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        using IServiceScope scope = CreateScope();
        ILdapService ldapService = scope.ServiceProvider.GetRequiredService<ILdapService>();

        // Act
        // 'Administrator' is the default user in Samba AD DC
        LdapUserResponse? result = await ldapService.GetUserAsync(
            "Administrator",
            TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldNotBeNull();
        result.Username.ShouldBe("Administrator");
        result.DistinguishedName!.ToLower().ShouldContain("dc=domain1,dc=sink,dc=test");
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnTrue_WhenCredentialsAreCorrect()
    {
        // Arrange
        using IServiceScope scope = CreateScope();
        ILdapService ldapService = scope.ServiceProvider.GetRequiredService<ILdapService>();

        var username = "Administrator";
        var password = "AdminPassword123";

        // Act
        ErrorOr.ErrorOr<LdapUserResponse> result = await ldapService.AuthenticateAsync(
            username,
            password,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Username.ShouldBe(username);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnFalse_WhenCredentialsAreWrong()
    {
        // Arrange
        using IServiceScope scope = CreateScope();
        ILdapService ldapService = scope.ServiceProvider.GetRequiredService<ILdapService>();

        var username = "Administrator";
        var password = "WrongPassword";

        // Act
        ErrorOr.ErrorOr<LdapUserResponse> result = await ldapService.AuthenticateAsync(
            username,
            password,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task LoginLdap_ShouldReturnOkAndSetCookie_WhenCredentialsAreCorrect()
    {
        // Arrange
        // Note: This requires the 'ldap' docker profile to be running (Samba AD DC)
        await SeedBootstrapTenantAsync();
        var request = new LdapLoginRequest("Administrator", "AdminPassword123");

        // Ensure user is active if it was created by another test
        using (IServiceScope scope = CreateScope())
        {
            IdentityDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            string normalizedUsername = NormalizedString.Normalize("Administrator");
            AppUser? user = await dbContext
                .Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    u => u.DbNormalizedUsername == normalizedUsername,
                    TestContext.Current.CancellationToken
                );
            if (user != null && !user.IsActive)
            {
                user.IsActive = true;
                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/bff/login/ldap",
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeTrue();
        response
            .Headers.GetValues("Set-Cookie")
            .ShouldContain(c => c.Contains(".APITemplate.Auth"));

        // Verify provisioning
        using IServiceScope verifyScope = CreateScope();
        IdentityDbContext verifyDbContext =
            verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        string verifyNormalizedUsername = NormalizedString.Normalize("Administrator");
        bool provisioned = await verifyDbContext
            .Users.IgnoreQueryFilters()
            .AnyAsync(
                u => u.DbNormalizedUsername == verifyNormalizedUsername,
                TestContext.Current.CancellationToken
            );
        provisioned.ShouldBeTrue();
    }

    [Fact]
    public async Task LoginLdap_ShouldReturnError_WhenLocalUserIsDisabled()
    {
        // Arrange
        await SeedBootstrapTenantAsync();
        var username = "Administrator";
        using (IServiceScope scope = CreateScope())
        {
            IdentityDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            string normalizedUsername = NormalizedString.Normalize(username);
            AppUser? user = await dbContext
                .Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    u => u.DbNormalizedUsername == normalizedUsername,
                    TestContext.Current.CancellationToken
                );
            if (user == null)
            {
                user = AppUser.Create(
                    username,
                    "admin@ldap.local",
                    null,
                    Guid.Parse(AuthConstants.Tenants.Bootstrap)
                );
                await dbContext.Users.AddAsync(user, TestContext.Current.CancellationToken);
            }
            user.IsActive = false;
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var request = new LdapLoginRequest(username, "AdminPassword123");

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/bff/login/ldap",
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem =
            await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(
                TestContext.Current.CancellationToken
            );
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("disabled");
    }
}
