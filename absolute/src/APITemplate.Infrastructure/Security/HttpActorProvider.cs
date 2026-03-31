using System.Security.Claims;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security;

/// <summary>
/// Resolves actor identity for auditing from the current HTTP principal.
/// </summary>
/// <remarks>
/// Uses a prioritized claim lookup and falls back to configured system identity
/// when no user claim is available (for example background/system execution paths).
/// </remarks>
public sealed class HttpActorProvider : IActorProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SystemIdentityOptions _systemIdentity;

    public HttpActorProvider(
        IHttpContextAccessor httpContextAccessor,
        IOptions<SystemIdentityOptions> systemIdentityOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _systemIdentity = systemIdentityOptions.Value;
    }

    public Guid ActorId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            // Prefer stable subject-style identifiers first, then name-like claims, then configured system fallback.
            var raw = user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue(AuthConstants.Claims.Subject)
                ?? user?.FindFirstValue(ClaimTypes.Name);

            return Guid.TryParse(raw, out var id) ? id : _systemIdentity.DefaultActorId;
        }
    }
}
