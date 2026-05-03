using System.Threading.RateLimiting;
using APITemplate.Tests.Integration.Helpers;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace APITemplate.Tests.Integration.Infrastructure;

public class RateLimitWebApplicationFactory : CustomWebApplicationFactory
{
    public const string TestIpHeader = "X-Test-IP";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting($"{RateLimitingOptions.Section}:Global:PermitLimit", "2");
        builder.UseSetting($"{RateLimitingOptions.Section}:Global:TokensPerPeriod", "2");
        builder.UseSetting($"{RateLimitingOptions.Section}:Global:WindowMinutes", "1");
        builder.UseSetting($"{RateLimitingOptions.Section}:Global:QueueLimit", "0");

        builder.UseSetting($"{RateLimitingOptions.Section}:Fixed:PermitLimit", "2");
        builder.UseSetting($"{RateLimitingOptions.Section}:Fixed:WindowMinutes", "1");
        builder.UseSetting($"{RateLimitingOptions.Section}:Fixed:QueueLimit", "0");

        builder.UseSetting($"{RateLimitingOptions.Section}:Sliding:PermitLimit", "2");
        builder.UseSetting($"{RateLimitingOptions.Section}:Sliding:WindowMinutes", "1");
        builder.UseSetting($"{RateLimitingOptions.Section}:Sliding:SegmentsPerWindow", "2");
        builder.UseSetting($"{RateLimitingOptions.Section}:Sliding:QueueLimit", "0");

        builder.ConfigureTestServices(services =>
        {
            // Register the test controller assembly so it's discovered
            services.AddControllers().AddApplicationPart(typeof(RateLimitTestController).Assembly);

            // Add extra policies for isolation in tests
            services.Configure<RateLimiterOptions>(limiter =>
            {
                limiter.AddPolicy(
                    RateLimitConstants.Policies.FixedTest1,
                    ctx =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetPartitionKey(ctx),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 2,
                                Window = TimeSpan.FromMinutes(1),
                            }
                        )
                );
                limiter.AddPolicy(
                    RateLimitConstants.Policies.FixedTest2,
                    ctx =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetPartitionKey(ctx),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 2,
                                Window = TimeSpan.FromMinutes(1),
                            }
                        )
                );
                limiter.AddPolicy(
                    RateLimitConstants.Policies.SlidingTest1,
                    ctx =>
                        RateLimitPartition.GetSlidingWindowLimiter(
                            partitionKey: GetPartitionKey(ctx),
                            factory: _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = 2,
                                Window = TimeSpan.FromMinutes(1),
                                SegmentsPerWindow = 2,
                            }
                        )
                );
            });

            // Add a middleware to simulate different IP addresses via a header
            services.Insert(
                0,
                ServiceDescriptor.Transient<IStartupFilter, RateLimitTestStartupFilter>()
            );
        });
    }

    private static string GetPartitionKey(HttpContext ctx)
    {
        IActorProvider actorProvider = ctx.RequestServices.GetRequiredService<IActorProvider>();
        Guid actorId = actorProvider.ActorId;

        return actorId != Guid.Empty
            ? actorId.ToString()
            : ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private sealed class RateLimitTestStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(
                    async (context, nextMiddleware) =>
                    {
                        if (context.Request.Headers.TryGetValue(TestIpHeader, out var ip))
                        {
                            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip!);
                        }
                        await nextMiddleware();
                    }
                );

                next(app);
            };
        }
    }
}
