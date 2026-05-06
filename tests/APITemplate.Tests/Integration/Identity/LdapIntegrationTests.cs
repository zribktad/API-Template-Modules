using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Domain.Interfaces;
using Identity.Auth.Features.Bff.DTOs;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.Directory.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Identity;

[Trait("Category", "Integration.Ldap")]
public sealed class LdapIntegrationTests : IntegrationTestBase<CustomWebApplicationFactory>
{
    public LdapIntegrationTests(CustomWebApplicationFactory factory)
        : base(factory) { }

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
        result.DistinguishedName!.ToLower().ShouldContain("dc=api-template,dc=local");
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
        var request = new LdapLoginRequest("Administrator", "AdminPassword123");

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
            .ShouldContain(c => c.Contains(".AspNetCore.Cookies"));

        // Verify provisioning
        using IServiceScope scope = CreateScope();
        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        bool provisioned = await userRepository.ExistsByUsernameAsync(
            "Administrator",
            TestContext.Current.CancellationToken
        );
        provisioned.ShouldBeTrue();
    }

    [Fact]
    public async Task LoginLdap_ShouldReturnError_WhenLocalUserIsDisabled()
    {
        // Arrange
        var username = "Administrator";
        using (IServiceScope scope = CreateScope())
        {
            IUserRepository userRepository =
                scope.ServiceProvider.GetRequiredService<IUserRepository>();
            AppUser? user = await userRepository.FirstOrDefaultAsync(
                new UserByUsernameSpecification(username),
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
                await userRepository.AddAsync(user, TestContext.Current.CancellationToken);
            }
            user.IsActive = false;
            await scope
                .ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken);
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
