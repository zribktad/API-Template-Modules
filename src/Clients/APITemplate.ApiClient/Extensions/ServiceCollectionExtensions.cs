using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace APITemplate.ApiClient.Extensions;

/// <summary>
///     Extension methods for registering the Kiota ApiClient in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the APITemplate Kiota ApiClient and its supporting request adapter and authentication services.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Delegate to configure <see cref="ApiClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiClient(
        this IServiceCollection services,
        Action<ApiClientOptions> configure
    )
    {
        services.Configure(configure);

        services.AddHttpClient<ApiClient>(
            (sp, httpClient) =>
            {
                ApiClientOptions options = sp.GetRequiredService<
                    IOptions<ApiClientOptions>
                >().Value;
                if (options.BaseUrl is not null)
                {
                    httpClient.BaseAddress = options.BaseUrl;
                }
                httpClient.Timeout = options.Timeout;
            }
        );

        services.AddScoped<IAuthenticationProvider>(sp =>
        {
            ApiClientOptions options = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;
            if (options.AccessTokenProvider is not null)
            {
                return new DelegateAuthenticationProvider(options.AccessTokenProvider);
            }

            return new AnonymousAuthenticationProvider();
        });

        services.AddScoped<IRequestAdapter>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            HttpClient httpClient = httpClientFactory.CreateClient(nameof(ApiClient));
            IAuthenticationProvider authProvider = sp.GetRequiredService<IAuthenticationProvider>();
            ApiClientOptions options = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;

            HttpClientRequestAdapter adapter = new(authProvider, httpClient: httpClient);
            if (options.BaseUrl is not null)
            {
                adapter.BaseUrl = options.BaseUrl.ToString().TrimEnd('/');
            }

            return adapter;
        });

        services.AddScoped<ApiClient>(sp =>
        {
            IRequestAdapter adapter = sp.GetRequiredService<IRequestAdapter>();
            return new ApiClient(adapter);
        });

        return services;
    }

    private sealed class DelegateAuthenticationProvider : IAuthenticationProvider
    {
        private readonly Func<CancellationToken, Task<string?>> _tokenProvider;

        public DelegateAuthenticationProvider(Func<CancellationToken, Task<string?>> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        public async Task AuthenticateRequestAsync(
            RequestInformation request,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default
        )
        {
            string? token = await _tokenProvider(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }
        }
    }
}
