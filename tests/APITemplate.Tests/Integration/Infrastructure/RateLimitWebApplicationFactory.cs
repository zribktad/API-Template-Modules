using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Http;

namespace APITemplate.Tests.Integration.Infrastructure;

public class RateLimitWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("RateLimiting:Global:PermitLimit", "2");
        builder.UseSetting("RateLimiting:Global:TokensPerPeriod", "2");
        builder.UseSetting("RateLimiting:Global:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:Global:QueueLimit", "0");

        builder.UseSetting("RateLimiting:Fixed:PermitLimit", "2");
        builder.UseSetting("RateLimiting:Fixed:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:Fixed:QueueLimit", "0");

        builder.UseSetting("RateLimiting:Sliding:PermitLimit", "2");
        builder.UseSetting("RateLimiting:Sliding:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:Sliding:SegmentsPerWindow", "2");
        builder.UseSetting("RateLimiting:Sliding:QueueLimit", "0");

        builder.ConfigureTestServices(services =>
        {
            // Register the test controller assembly so it's discovered
            services.AddControllers().AddApplicationPart(typeof(RateLimitTestController).Assembly);

            // Add extra policies for isolation in tests
            services.Configure<RateLimiterOptions>(limiter =>
            {
                var opts = services
                    .BuildServiceProvider()
                    .GetRequiredService<IOptions<RateLimitingOptions>>()
                    .Value;

                limiter.AddFixedWindowLimiter(
                    "fixed-test-1",
                    o =>
                    {
                        o.PermitLimit = 2;
                        o.Window = TimeSpan.FromMinutes(1);
                    }
                );
                limiter.AddFixedWindowLimiter(
                    "fixed-test-2",
                    o =>
                    {
                        o.PermitLimit = 2;
                        o.Window = TimeSpan.FromMinutes(1);
                    }
                );
                limiter.AddSlidingWindowLimiter(
                    "sliding-test-1",
                    o =>
                    {
                        o.PermitLimit = 2;
                        o.Window = TimeSpan.FromMinutes(1);
                        o.SegmentsPerWindow = 2;
                    }
                );
            });

            // Add a middleware to simulate different IP addresses via a header
            services.Insert(
                0,
                ServiceDescriptor.Transient<IStartupFilter, RateLimitTestStartupFilter>()
            );
        });
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
                        if (context.Request.Headers.TryGetValue("X-Test-IP", out var ip))
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
