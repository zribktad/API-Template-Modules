using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Auth.Entities;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Enums;
using Identity.Persistence;
using Identity.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace APITemplate.Tests.Integration;

internal static class IntegrationAuthHelper
{
    internal static readonly RSA RsaKey = RSA.Create(2048);

    internal static readonly RsaSecurityKey SecurityKey = new(RsaKey);

    private static readonly SigningCredentials _signingCredentials = new(
        SecurityKey,
        SecurityAlgorithms.RsaSha256
    );

    public static string CreateTestToken(
        Guid? userId = null,
        Guid? tenantId = null,
        string? username = null,
        UserRole role = UserRole.PlatformAdmin
    )
    {
        var id = userId ?? Guid.NewGuid();
        var tenant = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

        var claims = new List<Claim>
        {
            new(AuthConstants.Claims.Subject, id.ToString()),
            new(AuthConstants.Claims.PreferredUsername, username ?? "admin"),
            new(ClaimTypes.Email, $"{username ?? "admin"}@example.com"),
            new(AuthConstants.Claims.TenantId, tenant.ToString()),
            new(ClaimTypes.Role, role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "http://localhost:8180/realms/api-template",
            audience: "api-template",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void Authenticate(
        HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null,
        string? username = null,
        UserRole role = UserRole.PlatformAdmin
    )
    {
        string token = CreateTestToken(userId, tenantId, username, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static Guid AuthenticateAndGetUserId(
        HttpClient client,
        Guid? tenantId = null,
        string? username = null,
        UserRole role = UserRole.PlatformAdmin
    )
    {
        var userId = Guid.NewGuid();
        Authenticate(client, userId, tenantId, username, role);
        return userId;
    }

    public static void AuthenticateAsUser(
        HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null
    ) => Authenticate(client, userId, tenantId, username: "user", role: UserRole.User);

    public static void AuthenticateAsTenantAdmin(
        HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null
    ) =>
        Authenticate(client, userId, tenantId, username: "tenantadmin", role: UserRole.TenantAdmin);

    /// <summary>
    ///     Seeds an additional tenant and user for tests that need data beyond the bootstrap tenant.
    /// </summary>
    public static async Task<(Tenant Tenant, AppUser User)> SeedTenantUserAsync(
        IServiceProvider services,
        string username,
        string email,
        bool userIsActive = true,
        bool tenantIsActive = true,
        CancellationToken ct = default
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        string tenantCodeValue = $"t{Guid.NewGuid():N}"[..12];
        TenantCode tenantCode = TenantCode.FromPersistence(tenantCodeValue);
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            TenantId = Guid.Empty,
            Code = tenantCode,
            Name = $"Tenant {username}",
        };
        if (!tenantIsActive)
            tenant.Deactivate();

        Email emailVo = Email.FromPersistence(email);

        AppUser user = AppUser.Create(
            username,
            emailVo,
            $"kc-{Guid.NewGuid():N}",
            tenantId: tenantId,
            isActive: userIsActive
        );

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);

        return (tenant, user);
    }
}
