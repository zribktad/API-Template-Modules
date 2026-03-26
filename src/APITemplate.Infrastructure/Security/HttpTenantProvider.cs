using System.Security.Claims;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace APITemplate.Infrastructure.Security;

/// <summary>
/// Resolves tenant identity from the current authenticated HTTP principal.
/// </summary>
/// <remarks>
/// Reads the <c>tenant_id</c> claim and returns <see cref="Guid.Empty"/> when missing/invalid.
/// Intended for scoped, request-bound usage through <see cref="IHttpContextAccessor"/>.
/// </remarks>
public sealed class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var claimValue = _httpContextAccessor.HttpContext?.User.FindFirstValue(
                AuthConstants.Claims.TenantId
            );
            // Invalid or missing tenant claim is represented as Guid.Empty and treated as "no tenant".
            return Guid.TryParse(claimValue, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public bool HasTenant => TenantId != Guid.Empty;
}
