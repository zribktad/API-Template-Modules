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

        RateLimitingOptions opts =
            configuration.SectionFor<RateLimitingOptions>().Get<RateLimitingOptions>() ?? new();

        // Distributed Rate Limiting requires Redis and explicit opt-in via AllowDistributedRateLimiting.
        // If disabled or Redis is missing, we fall back to in-memory limiting.
        bool useRedis =
            opts.AllowDistributedRateLimiting
            && services.Any(d => d.ServiceType == typeof(IConnectionMultiplexer));

        services.AddRateLimiter(limiter =>
        {
            if (useRedis)
            {
                AddRedisPolicies(limiter, opts, services);
            }
            else
            {
                AddInMemoryPolicies(limiter, opts);
            }

            ConfigureOnRejected(limiter, opts);
        });

        return services;
    }

    private static void AddRedisPolicies(
        RateLimiterOptions limiter,
        RateLimitingOptions opts,
        IServiceCollection services
    )
    {
        // Connection multiplexer resolved lazily from the root provider to avoid BuildServiceProvider cycles.
        // We use BuildServiceProvider() once here during registration to get the factory,
        // which is acceptable for singleton-like infrastructure setup if guarded.
        IServiceProvider rootProvider = services.BuildServiceProvider();

        limiter.AddRedisFixedWindowLimiter(
            RateLimitPolicies.Fixed,
            opt =>
            {
                opt.PermitLimit = opts.Fixed.PermitLimit;
                opt.Window = TimeSpan.FromMinutes(opts.Fixed.WindowMinutes);
                opt.ConnectionMultiplexerFactory = () =>
                    rootProvider.GetRequiredService<IConnectionMultiplexer>();
            }
        );

        limiter.AddRedisSlidingWindowLimiter(
            RateLimitPolicies.Sliding,
            opt =>
            {
                opt.PermitLimit = opts.Sliding.PermitLimit;
                opt.Window = TimeSpan.FromMinutes(opts.Sliding.WindowMinutes);
                opt.ConnectionMultiplexerFactory = () =>
                    rootProvider.GetRequiredService<IConnectionMultiplexer>();
            }
        );

        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            IConnectionMultiplexer? redis =
                ctx.RequestServices.GetService<IConnectionMultiplexer>();
            string partitionKey = GetPartitionKey(ctx);

            if (redis is null)
            {
                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = opts.Global.PermitLimit,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(opts.Global.WindowMinutes),
                        TokensPerPeriod = opts.Global.TokensPerPeriod,
                        AutoReplenishment = true,
                        QueueLimit = opts.Global.QueueLimit,
                    }
                );
            }

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
