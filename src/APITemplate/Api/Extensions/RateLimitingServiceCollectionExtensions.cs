using System.Globalization;
using System.Threading.RateLimiting;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Http;
using BuildingBlocks.Web.Api;
using ErrorOr;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring rate limiting in the application.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the ASP.NET Core rate limiter with a global baseline limiter and specific named
    ///     policies for opt-in via [EnableRateLimiting]. All are driven by appsettings RateLimiting section.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the rate limiter to.</param>
    /// <param name="configuration">The IConfiguration to read settings from.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RateLimitingOptions>(configuration);

        RateLimitingOptions opts =
            configuration.SectionFor<RateLimitingOptions>().Get<RateLimitingOptions>() ?? new();

        services.AddRateLimiter(limiter =>
        {
            AddPartitionedFixedPolicy(limiter, opts.Fixed);
            AddPartitionedSlidingPolicy(limiter, opts.Sliding);
            AddGlobalLimiter(limiter, opts.Global);
            ConfigureOnRejected(limiter, opts);
        });

        return services;
    }

    /// <summary>
    ///     Adds a partitioned named rate limiting policy using a fixed window algorithm.
    ///     Requests are limited within a fixed time segment (e.g., 100 requests per minute).
    /// </summary>
    private static void AddPartitionedFixedPolicy(
        RateLimiterOptions limiter,
        RateLimitPolicyOptions opts
    )
    {
        // Named policy providing a fixed request budget per time window, partitioned by user.
        limiter.AddPolicy(
            RateLimitPolicies.Fixed,
            ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(ctx),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = opts.PermitLimit,
                        Window = TimeSpan.FromMinutes(opts.WindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = opts.QueueLimit,
                    }
                )
        );
    }

    /// <summary>
    ///     Adds a partitioned named rate limiting policy using a sliding window algorithm.
    ///     Provides a smoother experience by dividing the window into segments and releasing permits gradually.
    /// </summary>
    private static void AddPartitionedSlidingPolicy(
        RateLimiterOptions limiter,
        RateLimitPolicyOptions opts
    )
    {
        // Named policy with segmented window to prevent traffic spikes at window boundaries, partitioned by user.
        limiter.AddPolicy(
            RateLimitPolicies.Sliding,
            ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: GetPartitionKey(ctx),
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = opts.PermitLimit,
                        Window = TimeSpan.FromMinutes(opts.WindowMinutes),
                        SegmentsPerWindow = opts.SegmentsPerWindow,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = opts.QueueLimit,
                    }
                )
        );
    }

    /// <summary>
    ///     Configures a global rate limiter using a token bucket algorithm.
    ///     This limiter applies to all requests and allows for short bursts of traffic while maintaining a steady refill rate.
    ///     Identifies users by their IActorProvider identity or falls back to Remote IP address.
    /// </summary>
    private static void AddGlobalLimiter(RateLimiterOptions limiter, RateLimitPolicyOptions opts)
    {
        // Global baseline partitioned by authenticated user ID or remote IP address.
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: GetPartitionKey(ctx),
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = opts.PermitLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(opts.WindowMinutes),
                    TokensPerPeriod = opts.TokensPerPeriod,
                    AutoReplenishment = true,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = opts.QueueLimit,
                }
            )
        );
    }

    /// <summary>
    ///     Extracts a partition key from the HttpContext based on user identity or remote IP address.
    /// </summary>
    private static string GetPartitionKey(HttpContext ctx)
    {
        IActorProvider actorProvider = ctx.RequestServices.GetRequiredService<IActorProvider>();
        Guid actorId = actorProvider.ActorId;

        return actorId != Guid.Empty
            ? actorId.ToString()
            : ctx.Connection.RemoteIpAddress?.ToString() ?? RateLimitConstants.FallbackPartitionKey;
    }

    /// <summary>
    ///     Configures the behavior when a request is rejected by the rate limiter.
    ///     Returns a 429 Too Many Requests response with standard RateLimit headers.
    /// </summary>
    private static void ConfigureOnRejected(RateLimiterOptions limiter, RateLimitingOptions opts)
    {
        // Generates a RFC-compliant 429 response with appropriate RateLimit headers.
        limiter.OnRejected = async (context, _) =>
        {
            HttpResponse response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            {
                response.Headers[HeaderNames.RetryAfter] = ((int)retryAfter.TotalSeconds).ToString(
                    NumberFormatInfo.InvariantInfo
                );
            }

            string policyName =
                context
                    .HttpContext.GetEndpoint()
                    ?.Metadata.GetMetadata<EnableRateLimitingAttribute>()
                    ?.PolicyName
                ?? RateLimitConstants.GlobalPolicy;

            response.Headers[RateLimitConstants.Headers.Policy] = policyName;

            string limit = policyName switch
            {
                RateLimitPolicies.Fixed => opts.Fixed.PermitLimit.ToString(
                    CultureInfo.InvariantCulture
                ),
                RateLimitPolicies.Sliding => opts.Sliding.PermitLimit.ToString(
                    CultureInfo.InvariantCulture
                ),
                _ => opts.Global.PermitLimit.ToString(CultureInfo.InvariantCulture),
            };

            response.Headers[RateLimitConstants.Headers.Limit] = limit;

            Error error = Error.Failure(
                ErrorCatalog.General.RateLimitExceeded,
                "Too many requests. Please try again later."
            );

            IProblemDetailsService problemDetailsService =
                context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

            await problemDetailsService.TryWriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = error.ToProblemDetails(context.HttpContext),
                }
            );
        };
    }
}
