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
            return field;
        }
        private set;
    }

    /// <inheritdoc />
    public string? PreferredUsername
    {
        get
        {
            EnsureComputed();
            return field;
        }
        private set;
    }

    /// <inheritdoc />
    public bool IsInteractiveUser
    {
        get
        {
            EnsureComputed();
            return field;
        }
        private set;
    } = true;

    /// <inheritdoc cref="IActorProvider.ActorId" />
    public Guid ActorId
    {
        get
        {
            EnsureComputed();
            return field;
        }
        private set;
    }

    private void EnsureComputed()
    {
        if (_computed)
            return;

        _computed = true;

        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            ActorId = Guid.Empty;
            return;
        }

        string? nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        _oidcSubject =
            user.FindFirstValue(AuthConstants.Claims.Subject)
            ?? user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        PreferredUsername =
            user.FindFirstValue(AuthConstants.Claims.PreferredUsername)
            ?? user.FindFirstValue(ClaimTypes.Name);

        if (KeycloakServiceAccountClaims.IsServiceAccount(user))
            IsInteractiveUser = false;

        if (Guid.TryParse(nameId, out Guid nameIdGuid))
        {
            if (
                _oidcSubject is null
                || !string.Equals(nameId, _oidcSubject, StringComparison.Ordinal)
            )
                ApplicationUserId = nameIdGuid;
        }

        // Match legacy HttpActorProvider: NameIdentifier → Subject → Name (no Jwt sub in this chain).
        string? actorRaw =
            nameId
            ?? user.FindFirstValue(AuthConstants.Claims.Subject)
            ?? user.FindFirstValue(ClaimTypes.Name);

        ActorId = Guid.TryParse(actorRaw, out Guid parsed) ? parsed : Guid.Empty;
    }
}
