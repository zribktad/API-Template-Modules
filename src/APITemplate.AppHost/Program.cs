using JasperFx.Aspire;
using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
builder.Configuration["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true";
Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

// ── PostgreSQL ─────────────────────────────────────────────────────────────
IResourceBuilder<ParameterResource> postgresPassword = builder.AddParameter(
    "postgres-password",
    "postgres"
);
IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres", password: postgresPassword)
    .WithImageTag("18.3")
    .WithDataVolume("apitemplate-postgres-data")
    .WithHostPort(5432)
    .WithBindMount(
        "../../infrastructure/postgres/init-keycloak-db.sql",
        "/docker-entrypoint-initdb.d/init-keycloak-db.sql"
    );

IResourceBuilder<PostgresDatabaseResource> apitemplateDb = postgres.AddDatabase(
    "postgres-db",
    "apitemplate"
);

// ── Dragonfly (Redis-compatible) ───────────────────────────────────────────
IResourceBuilder<ContainerResource> dragonfly = builder
    .AddContainer("dragonfly", "docker.dragonflydb.io/dragonflydb/dragonfly", "v1.27.1")
    .WithArgs("dragonfly", "--maxmemory", "512mb", "--proactor_threads", "2", "--cache_mode=true")
    .WithHttpEndpoint(targetPort: 6379, port: 6379, name: "redis");

// ── MongoDB ────────────────────────────────────────────────────────────────
IResourceBuilder<MongoDBServerResource> mongodb = builder
    .AddMongoDB("mongodb")
    .WithImageTag("8.2")
    .WithDataVolume("apitemplate-mongo-data")
    .WithEndpoint(targetPort: 27017, port: 27017, name: "tcp");

IResourceBuilder<MongoDBDatabaseResource> mongoDb = mongodb.AddDatabase("mongo-db", "apitemplate");

// ── Keycloak ───────────────────────────────────────────────────────────────
IResourceBuilder<ContainerResource> keycloak = builder
    .AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.5")
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL", "jdbc:postgresql://postgres:5432/keycloak")
    .WithEnvironment("KC_DB_USERNAME", "postgres")
    .WithEnvironment("KC_DB_PASSWORD", "postgres")
    .WithEnvironment("KC_HTTP_PORT", "8180")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
    .WithHttpEndpoint(targetPort: 8180, port: 8180, name: "http")
    .WithUrlForEndpoint(
        "http",
        _ => new() { Url = "http://localhost:8180/admin", DisplayText = "Keycloak Admin Console" }
    )
    .WithUrlForEndpoint(
        "http",
        _ =>
            new()
            {
                Url = "http://localhost:8180/realms/api-template/.well-known/openid-configuration",
                DisplayText = "OIDC Discovery",
            }
    )
    .WithBindMount("../../infrastructure/keycloak/realms", "/opt/keycloak/data/import")
    .WaitFor(postgres);

// ── Mailpit (SMTP & Web UI) ────────────────────────────────────────────────
IResourceBuilder<ContainerResource> mailpit = builder
    .AddContainer("mailpit", "axllent/mailpit", "v1.29.0")
    .WithHttpEndpoint(targetPort: 8025, port: 8025, name: "ui")
    .WithEndpoint(targetPort: 1025, port: 1025, name: "smtp")
    .WithUrlForEndpoint(
        "ui",
        _ => new() { Url = "http://localhost:8025", DisplayText = "Mailpit Mailbox" }
    );

// ── APITemplate API Monolith ───────────────────────────────────────────────
IResourceBuilder<ProjectResource> api = builder
    .AddProject<APITemplate>("api")
    .WithReference(apitemplateDb)
    .WithReference(mongoDb)
    .WaitFor(postgres)
    .WaitFor(mongodb)
    .WaitFor(dragonfly)
    .WaitFor(keycloak)
    .WaitFor(mailpit)
    .WithHttpHealthCheck("/health/live")
    .WithUrlForEndpoint(
        "http",
        _ => new() { Url = "http://localhost:5174/scalar", DisplayText = "Scalar API Docs" }
    )
    .WithUrlForEndpoint(
        "http",
        _ =>
            new() { Url = "http://localhost:5174/graphql", DisplayText = "GraphQL Banana Cake Pop" }
    )
    .WithUrlForEndpoint(
        "http",
        _ => new() { Url = "http://localhost:5174/health", DisplayText = "Health Checks Details" }
    )
    .WithUrlForEndpoint(
        "http",
        _ => new() { Url = "http://localhost:5174/health/live", DisplayText = "Liveness Probe" }
    )
    .WithUrlForEndpoint(
        "http",
        _ => new() { Url = "http://localhost:5174/health/ready", DisplayText = "Readiness Probe" }
    )
    .WithEnvironment("Observability__Exporters__Otlp__Enabled", "true")
    .WithEnvironment("Observability__Otlp__Endpoint", "http://localhost:18890")
    .WithEndpoint("http", endpoint => endpoint.Port = 5174)
    .WithJasperFxCommands(opts =>
    {
        opts.DiscoverCommands = true;
        opts.IncludeMutatingCommands = true;
    });

builder.Build().Run();
