namespace SharedKernel.Infrastructure.Health;

public static class HealthCheckTags
{
    public const string Live = "live";
    public const string Ready = "ready";
    public const string Database = "db";
    public const string Cache = "cache";
    public const string External = "external";
    public const string OpenApiTag = "Health";
}
