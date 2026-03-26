namespace APITemplate.Application.Common.Options;

/// <summary>
/// Top-level application options that apply globally across the service.
/// </summary>
public sealed class AppOptions
{
    public string ServiceName { get; init; } = "APITemplate";
}
