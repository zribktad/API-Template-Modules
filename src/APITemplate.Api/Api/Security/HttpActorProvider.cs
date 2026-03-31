using System.Security.Claims;
using Identity.Application.Common.Security;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Security;

public sealed class HttpActorProvider(IHttpContextAccessor httpContextAccessor) : IActorProvider
{
    public Guid ActorId
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            string? raw =
                user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue(AuthConstants.Claims.Subject)
                ?? user?.FindFirstValue(ClaimTypes.Name);

            return Guid.TryParse(raw, out Guid actorId) ? actorId : Guid.Empty;
        }
    }
}
