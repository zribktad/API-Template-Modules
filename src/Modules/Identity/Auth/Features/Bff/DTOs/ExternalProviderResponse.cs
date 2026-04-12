namespace Identity.Auth.Features.Bff.DTOs;

/// <summary>
///     Represents an external identity provider available for social login.
/// </summary>
public sealed record ExternalProviderResponse(string IdpHint, string DisplayName);
