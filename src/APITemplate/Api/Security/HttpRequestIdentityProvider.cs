using System.Security.Claims;
using Identity.Auth.Security;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Security;

/// <summary>
///     Single per-request source for <see cref="ICurrentRequestUser" /> and <see cref="IActorProvider" /> derived from
///     <see cref="Microsoft.AspNetCore.Http.HttpContext.User" />.
/// </summary>
public sealed class HttpRequestIdentityProvider : ICurrentRequestUser, IActorProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private bool _computed;
    private string? _oidcSubject;
    private Guid? _applicationUserId;
    private string? _preferredUsername;
    private bool _isInteractiveUser = true;
    private Guid _actorId;

    public HttpRequestIdentityProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string? OidcSubject
    {
        get
        {
            EnsureComputed();
            return _oidcSubject;
        }
    }

    /// <inheritdoc />
    public Guid? ApplicationUserId
    {
        get
        {
            EnsureComputed();
            return _applicationUserId;
        }
    }

    /// <inheritdoc />
    public string? PreferredUsername
    {
        get
        {
            EnsureComputed();
            return _preferredUsername;
        }
    }

    /// <inheritdoc />
    public bool IsInteractiveUser
    {
        get
        {
            EnsureComputed();
            return _isInteractiveUser;
        }
    }

    /// <inheritdoc cref="IActorProvider.ActorId" />
    public Guid ActorId
    {
        get
        {
            EnsureComputed();
            return _actorId;
        }
    }

    private void EnsureComputed()
    {
        if (_computed)
            return;

        _computed = true;

        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            _actorId = Guid.Empty;
            return;
        }

        string? nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        _oidcSubject =
            user.FindFirstValue(AuthConstants.Claims.Subject)
            ?? user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        _preferredUsername =
            user.FindFirstValue(AuthConstants.Claims.PreferredUsername)
            ?? user.FindFirstValue(ClaimTypes.Name);

        if (KeycloakServiceAccountClaims.IsServiceAccount(user))
            _isInteractiveUser = false;

        if (Guid.TryParse(nameId, out Guid nameIdGuid))
        {
            if (
                _oidcSubject is null
                || !string.Equals(nameId, _oidcSubject, StringComparison.Ordinal)
            )
                _applicationUserId = nameIdGuid;
        }

        // Match legacy HttpActorProvider: NameIdentifier → Subject → Name (no Jwt sub in this chain).
        string? actorRaw =
            nameId
            ?? user.FindFirstValue(AuthConstants.Claims.Subject)
            ?? user.FindFirstValue(ClaimTypes.Name);

        _actorId = Guid.TryParse(actorRaw, out Guid parsed) ? parsed : Guid.Empty;
    }
}
