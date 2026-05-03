namespace BuildingBlocks.Web.Health;

public static class HealthCheckNames
{
    public const string PostgreSql = "postgresql";
    public const string MongoDb = "mongodb";
    public const string Keycloak = "keycloak";
    public const string Redis = "redis";
    public const string WolverineMessageStore = "wolverine-message-store";
    public const string WolverineDeadLetters = "wolverine-dead-letters";
    public const string Smtp = "smtp";
    public const string OtlpCollector = "otlp-collector";
}

