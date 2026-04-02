namespace APITemplate.Application.Common.Options.Security;

/// <summary>
/// Configuration for the CORS policy, listing the origins that are permitted to make cross-origin requests.
/// </summary>
public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; init; } = [];
}
