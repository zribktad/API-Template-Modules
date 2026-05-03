namespace BuildingBlocks.Web.Observability;

/// <summary>Shared names for the application's OpenTelemetry activity source and meters.</summary>
public static class ObservabilityConventions
{
    public const string ActivitySourceName = "APITemplate";
    public const string MeterName = "APITemplate";
    public const string HealthMeterName = "APITemplate.Health";
}

/// <summary>Canonical tag/attribute key names applied to metrics and traces.</summary>
public static class TelemetryTagKeys
{
    public const string ApiSurface = "apitemplate.api.surface";
    public const string Authenticated = "apitemplate.authenticated";
    public const string TenantId = "tenant.id";
    public const string AuthScheme = "auth.scheme";
    public const string AuthFailureReason = "auth.failure_reason";
    public const string CacheOutcome = "cache.outcome";
    public const string CachePolicy = "cache.policy";
    public const string CacheTag = "cache.tag";
    public const string DbSystem = "db.system";
    public const string DbOperation = "db.operation";
    public const string DbStoredProcedureName = "db.stored_procedure.name";
    public const string DbResultCount = "db.result_count";
    public const string ErrorCode = "error.code";
    public const string ExceptionType = "exception.type";
    public const string GraphQlCostKind = "graphql.cost.kind";
    public const string GraphQlHasErrors = "graphql.has_errors";
    public const string GraphQlOperationType = "graphql.operation.type";
    public const string GraphQlPhase = "graphql.phase";
    public const string HttpMethod = "http.request.method";
    public const string HttpResponseStatusCode = "http.response.status_code";
    public const string HttpRoute = "http.route";
    public const string RateLimitPolicy = "rate_limit.policy";
    public const string Service = "service";
    public const string StartupComponent = "startup.component";
    public const string StartupStep = "startup.step";
    public const string StartupSuccess = "startup.success";
    public const string ValidationDtoType = "validation.dto_type";
    public const string ValidationProperty = "validation.property";
}

/// <summary>Well-known tag values that identify the API surface a request was served from.</summary>
public static class TelemetrySurfaces
{
    public const string Bff = "bff";
    public const string Documentation = "documentation";
    public const string GraphQl = "graphql";
    public const string Health = "health";
    public const string Rest = "rest";
}

/// <summary>URL path prefixes used to classify requests into API surface areas.</summary>
public static class TelemetryPathPrefixes
{
    public const string GraphQl = "/graphql";
    public const string Health = "/health";
    public const string OpenApi = "/openapi";
    public const string Scalar = "/scalar";
}

/// <summary>Default fallback values used when a tag or setting cannot be resolved.</summary>
public static class TelemetryDefaults
{
    public const string Default = "default";
    public const string Sql = "sql";
    public const string Unknown = "unknown";
}

