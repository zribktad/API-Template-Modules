using Microsoft.Extensions.Compliance.Redaction;

namespace APITemplate.Api.Extensions.Startup;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddApplicationRedaction(this WebApplicationBuilder builder)
    {
        builder.Services.AddRedaction();
        builder.Logging.EnableRedaction();
        return builder;
    }
}
