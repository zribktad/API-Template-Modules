using System.Threading.RateLimiting;
using BuildingBlocks.Application.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace APITemplate.Api.Middleware;

public sealed class IpRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PartitionedRateLimiter<HttpContext> _limiter;
    private readonly ILogger<IpRateLimitMiddleware> _logger;

    public IpRateLimitMiddleware(RequestDelegate next, ILogger<IpRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        // A cheap, local TokenBucket rate limiter keyed by IP address
        // Acts as a first line of defense before Authentication and heavier logic.
        _limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetTokenBucketLimiter(
                ip,
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 200,
                    TokensPerPeriod = 200,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            );
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using RateLimitLease lease = await _limiter.AcquireAsync(
            context,
            1,
            context.RequestAborted
        );
        if (!lease.IsAcquired)
        {
            _logger.LogWarning(
                "IP Rate limit exceeded for {IpAddress}",
                context.Connection.RemoteIpAddress
            );
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Rate limit exceeded",
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Detail = "Too many requests from this IP address. Please try again later.",
            };
            problemDetails.Extensions["errorCode"] = ErrorCatalog.General.RateLimitExceeded;

            await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
