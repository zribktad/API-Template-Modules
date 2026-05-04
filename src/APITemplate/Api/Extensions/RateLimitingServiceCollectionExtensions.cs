using System.Globalization;
using System.Threading.RateLimiting;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Http;
using BuildingBlocks.Application.Options;
using BuildingBlocks.Web.Api;
using ErrorOr;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RedisRateLimiting;
using RedisRateLimiting.AspNetCore;
using StackExchange.Redis;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring rate limiting in the application.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the ASP.NET Core rate limiter with a global baseline limiter and specific named
    ///     policies for opt-in via [EnableRateLimiting]. All are driven by appsettings RateLimiting section.
    ///     Uses Redis as the backplane for distributed state management if configured and enabled.
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
        services.AddRateLimiter();
        services.ConfigureOptions<RateLimiterOptionsSetup>();
        return services;
    }

    /// <summary>
    ///     A class automatically invoked by the ASP.NET Core DI container (via the Options pattern)
    ///     during Rate Limiter initialization. It ensures dynamic switching between
    ///     distributed (Redis) and local (In-Memory) modes.
    /// </summary>
    private sealed class RateLimiterOptionsSetup(
        IOptions<RateLimitingOptions> options,
        IConfiguration configuration
    ) : IConfigureOptions<RateLimiterOptions>
    {
        /// <summary>
        ///     The main configuration method. It populates an empty <see cref="RateLimiterOptions"/>
        ///     object with policies based on the settings from appsettings.json.
        /// </summary>
        public void Configure(RateLimiterOptions limiter)
        {
            RateLimitingOptions opts = options.Value;
            // Check whether we want and can use Redis as a shared backplane for all API instances
            bool useRedis = opts.AllowDistributedRateLimiting && configuration.IsRedisConfigured();

            // Register corresponding policy implementations (Global, Fixed, Sliding)
            if (useRedis)
            {
                AddRedisPolicies(limiter, opts);
            }
            else
            {
                AddInMemoryPolicies(limiter, opts);
            }

            // Configure behavior upon limit violation (returns 429 Too Many Requests in RFC 7807 format)
            ConfigureOnRejected(limiter, opts);
        }
    }

    private static void AddRedisPolicies(RateLimiterOptions limiter, RateLimitingOptions opts)
    {
        // Named policy providing a distributed fixed request budget per time window.
        limiter.AddPolicy(
            RateLimitPolicies.Fixed,
            ctx =>
            {
                IConnectionMultiplexer redis =
                    ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                return RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    partitionKey: GetPartitionKey(ctx),
                    factory: _ => new RedisFixedWindowRateLimiterOptions
                    {
                        PermitLimit = opts.Fixed.PermitLimit,
                        Window = TimeSpan.FromMinutes(opts.Fixed.WindowMinutes),
                        ConnectionMultiplexerFactory = () => redis,
                    }
                );
            }
        );

        // Named policy providing a distributed sliding request budget.
        limiter.AddPolicy(
            RateLimitPolicies.Sliding,
            ctx =>
            {
                IConnectionMultiplexer redis =
                    ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                return RedisRateLimitPartition.GetSlidingWindowRateLimiter(
                    partitionKey: GetPartitionKey(ctx),
                    factory: _ => new RedisSlidingWindowRateLimiterOptions
                    {
                        PermitLimit = opts.Sliding.PermitLimit,
                        Window = TimeSpan.FromMinutes(opts.Sliding.WindowMinutes),
                        ConnectionMultiplexerFactory = () => redis,
                    }
                );
            }
        );

        // Global baseline partitioned by authenticated user ID or remote IP address.
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            IConnectionMultiplexer redis =
                ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            string partitionKey = GetPartitionKey(ctx);

            return RedisRateLimitPartition.GetTokenBucketRateLimiter(
                partitionKey: partitionKey,
                factory: _ => new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = opts.Global.PermitLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(opts.Global.WindowMinutes),
                    TokensPerPeriod = opts.Global.TokensPerPeriod,
                    ConnectionMultiplexerFactory = () => redis,
                }
            );
        });
    }

    private static void AddInMemoryPolicies(RateLimiterOptions limiter, RateLimitingOptions opts)
    {
        limiter.AddFixedWindowLimiter(
            RateLimitPolicies.Fixed,
            opt =>
            {
                opt.PermitLimit = opts.Fixed.PermitLimit;
                opt.Window = TimeSpan.FromMinutes(opts.Fixed.WindowMinutes);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = opts.Fixed.QueueLimit;
            }
        );

        limiter.AddSlidingWindowLimiter(
            RateLimitPolicies.Sliding,
            opt =>
            {
                opt.PermitLimit = opts.Sliding.PermitLimit;
                opt.Window = TimeSpan.FromMinutes(opts.Sliding.WindowMinutes);
                opt.SegmentsPerWindow = opts.Sliding.SegmentsPerWindow;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = opts.Sliding.QueueLimit;
            }
        );

        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: GetPartitionKey(ctx),
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = opts.Global.PermitLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(opts.Global.WindowMinutes),
                    TokensPerPeriod = opts.Global.TokensPerPeriod,
                    AutoReplenishment = true,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = opts.Global.QueueLimit,
                }
            )
        );
    }

    /// <summary>
    ///     Extracts a partition key from the HttpContext based on user identity or remote IP address.
    /// </summary>
    private static string GetPartitionKey(HttpContext ctx)
    {
        IActorProvider? actorProvider = ctx.RequestServices.GetService<IActorProvider>();
        Guid actorId = actorProvider?.ActorId ?? Guid.Empty;

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
