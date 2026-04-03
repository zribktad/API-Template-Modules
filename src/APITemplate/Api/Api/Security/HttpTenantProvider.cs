using System.Security.Claims;
using Identity.Common.Security;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Security;

public sealed class HttpTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public Guid TenantId
    {
        get
        {
            string? claimValue = httpContextAccessor.HttpContext?.User.FindFirstValue(
                AuthConstants.Claims.TenantId
            );

            return Guid.TryParse(claimValue, out Guid tenantId) ? tenantId : Guid.Empty;
        }
    }

    public bool HasTenant => TenantId != Guid.Empty;
}
