namespace SharedKernel.Infrastructure.Health;

public static class HealthCheckNames
{
    public const string PostgreSql = "postgresql";
    public const string MongoDb = "mongodb";
    public const string Keycloak = "keycloak";
    public const string Dragonfly = "dragonfly";
    public const string WolverineMessageStore = "wolverine-message-store";
    public const string WolverineDeadLetters = "wolverine-dead-letters";
}
