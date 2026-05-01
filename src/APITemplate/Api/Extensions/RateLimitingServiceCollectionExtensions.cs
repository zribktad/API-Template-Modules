using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ErrorOr;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Http;
using SharedKernel.Contracts.Api;
using SharedKernel.Infrastructure.Configuration;

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
            AddFixedPolicy(limiter, opts.Fixed);
            AddSlidingPolicy(limiter, opts.Sliding);
            AddGlobalLimiter(limiter, opts.Global);
            ConfigureOnRejected(limiter, opts);
        });

        return services;
    }

    /// <summary>
    ///     Adds a named rate limiting policy using a fixed window algorithm.
    ///     Requests are limited within a fixed time segment (e.g., 100 requests per minute).
    /// </summary>
    private static void AddFixedPolicy(RateLimiterOptions limiter, RateLimitPolicyOptions opts)
    {
        // Simple window-based budget.
        limiter.AddFixedWindowLimiter(
            RateLimitPolicies.Fixed,
            o =>
            {
                o.PermitLimit = opts.PermitLimit;
                o.Window = TimeSpan.FromMinutes(opts.WindowMinutes);
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = opts.QueueLimit;
            }
        );
    }

    /// <summary>
    ///     Adds a named rate limiting policy using a sliding window algorithm.
    ///     Provides a smoother experience by dividing the window into segments and releasing permits gradually.
    /// </summary>
    private static void AddSlidingPolicy(RateLimiterOptions limiter, RateLimitPolicyOptions opts)
    {
        // Smoother window-based budget divided into segments.
        limiter.AddSlidingWindowLimiter(
            RateLimitPolicies.Sliding,
            o =>
            {
                o.PermitLimit = opts.PermitLimit;
                o.Window = TimeSpan.FromMinutes(opts.WindowMinutes);
                o.SegmentsPerWindow = opts.SegmentsPerWindow;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = opts.QueueLimit;
            }
        );
    }

    /// <summary>
    ///     Configures a global rate limiter using a token bucket algorithm.
    ///     This limiter applies to all requests and allows for short bursts of traffic while maintaining a steady refill rate.
    ///     Identifies users by their NameIdentifier claim or falls back to Remote IP address.
    /// </summary>
    private static void AddGlobalLimiter(RateLimiterOptions limiter, RateLimitPolicyOptions opts)
    {
        // Baseline budget that allows for bursts while maintaining a steady refill.
        // Partitioned by authenticated user ID or IP address.
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? ctx.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
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
    ///     Configures the behavior when a request is rejected by the rate limiter.
    ///     Returns a 429 Too Many Requests response with standard RateLimit headers.
    /// </summary>
    private static void ConfigureOnRejected(RateLimiterOptions limiter, RateLimitingOptions opts)
    {
        // Builds the 429 response. Runs after the limiter has already rejected the request.
        limiter.OnRejected = async (context, token) =>
        {
            HttpResponse response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(
                    NumberFormatInfo.InvariantInfo
                );

            string policyName =
                context
                    .HttpContext.GetEndpoint()
                    ?.Metadata.GetMetadata<EnableRateLimitingAttribute>()
                    ?.PolicyName
                ?? "global";

            response.Headers["RateLimit-Policy"] = policyName;
            response.Headers["RateLimit-Limit"] = policyName switch
            {
                RateLimitPolicies.Fixed => opts.Fixed.PermitLimit.ToString(),
                RateLimitPolicies.Sliding => opts.Sliding.PermitLimit.ToString(),
                _ => opts.Global.PermitLimit.ToString(),
            };

            Error error = Error.Custom(
                (int)ErrorType.Failure,
                ErrorCatalog.General.RateLimitExceeded,
                "Too many requests. Please try again later."
            );

            await response.WriteAsJsonAsync(
                error.ToProblemDetails(context.HttpContext),
                context
                    .HttpContext.RequestServices.GetRequiredService<
                        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>
                    >()
                    .Value.SerializerOptions,
                token
            );
        };
    }
}
