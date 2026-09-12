namespace APITemplate.ApiClient;

/// <summary>
///     Configuration options for the APITemplate Kiota API Client.
/// </summary>
public sealed class ApiClientOptions
{
    /// <summary>
    ///     Gets or sets the base URI for the API.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    ///     Gets or sets an optional bearer token or token provider callback.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    ///     Gets or sets the request timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
