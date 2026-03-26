using System.Collections.Concurrent;
using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace APITemplate.Api.Authorization;

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _cache = new();

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!Permission.All.Contains(policyName))
            return _fallback.GetPolicyAsync(policyName);

        var policy = _cache.GetOrAdd(
            policyName,
            name =>
                new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme,
                    AuthConstants.BffSchemes.Cookie
                )
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(name))
                    .Build()
        );

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
