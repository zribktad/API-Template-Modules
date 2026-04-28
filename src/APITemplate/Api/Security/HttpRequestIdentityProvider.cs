using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Auth.Security;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Security;

/// <summary>
///     Single per-request source for <see cref="ICurrentRequestUser" />, <see cref="IActorProvider" />,
///     and <see cref="ITenantProvider" /> derived from <see cref="Microsoft.AspNetCore.Http.HttpContext.User" />.
///     All claims are parsed once on first access and cached in <see cref="IdentitySnapshot" />.
/// </summary>
public sealed class HttpRequestIdentityProvider
    : ICurrentRequestUser,
        IActorProvider,
        ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private IdentitySnapshot? _snapshot;

    public HttpRequestIdentityProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? OidcSubject => GetSnapshot().OidcSubject;
    public Guid? ApplicationUserId => GetSnapshot().ApplicationUserId;
    public string? PreferredUsername => GetSnapshot().PreferredUsername;
    public bool IsInteractiveUser => GetSnapshot().IsInteractiveUser;
    public Guid ActorId => GetSnapshot().ActorId;
    public Guid TenantId => GetSnapshot().TenantId;
    public bool HasTenant => TenantId != Guid.Empty;

    private IdentitySnapshot GetSnapshot()
    {
        if (_snapshot.HasValue)
            return _snapshot.Value;
        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
            return new IdentitySnapshot();
        return (_snapshot = Compute(user)).Value;
    }

    private static IdentitySnapshot Compute(ClaimsPrincipal user)
    {
        string? nameId =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.NameId);
        string? subject =
            user.FindFirstValue(AuthConstants.Claims.Subject)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        string? name = user.FindFirstValue(ClaimTypes.Name);

        Guid? appUserId =
            Guid.TryParse(nameId, out Guid g)
            && (subject is null || !string.Equals(nameId, subject, StringComparison.Ordinal))
                ? g
                : null;

        return new IdentitySnapshot(
            OidcSubject: subject,
            ApplicationUserId: appUserId,
            PreferredUsername: user.FindFirstValue(AuthConstants.Claims.PreferredUsername) ?? name,
            IsInteractiveUser: !KeycloakServiceAccountClaims.IsServiceAccount(user),
            ActorId: Guid.TryParse(nameId ?? subject, out Guid actor) ? actor : Guid.Empty,
            TenantId: Guid.TryParse(
                user.FindFirstValue(AuthConstants.Claims.TenantId),
                out Guid tid
            )
                ? tid
                : Guid.Empty
        );
    }

    private readonly record struct IdentitySnapshot(
        string? OidcSubject = null,
        Guid? ApplicationUserId = null,
        string? PreferredUsername = null,
        bool IsInteractiveUser = true,
        Guid ActorId = default,
        Guid TenantId = default
    );
}
