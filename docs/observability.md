# Observability

## Quick Connection URLs

Use these URLs when connecting the API or opening the observability services locally:

| Service                           | URL                                                        |
| --------------------------------- | ---------------------------------------------------------- |
| API (VS Code local launch)        | `http://localhost:5174`                                    |
| API health (VS Code local launch) | `http://localhost:5174/health`                             |
| Grafana                           | `http://localhost:3001`                                    |
| Prometheus                        | `http://localhost:9090`                                    |
| Loki                              | `http://localhost:3100`                                    |
| Tempo                             | `http://localhost:3200`                                    |
| Aspire Dashboard UI               | `http://localhost:18888` when an Aspire profile is running |
| OTLP gRPC endpoint                | `http://localhost:4317`                                    |
| OTLP HTTP endpoint                | `http://localhost:4318`                                    |

For local API telemetry export:

- use `http://localhost:4317` when sending to `Aspire Dashboard` in Aspire-only mode
- use `http://localhost:18889` when sending to `Aspire Dashboard` in full observability mode
- use `http://localhost:4317` when sending to `Grafana Alloy`
- do not run `Aspire Dashboard` and `Alloy` on the same host OTLP port mapping at the same time unless one is remapped

If you start the API from VS Code using `.NET API + Observability`, the API is available on `http://localhost:5174` and the observability UI is Grafana on `http://localhost:3001`.
If you start the API from VS Code using `.NET API + Full Observability`, the API is available on `http://localhost:5174`, Grafana is on `http://localhost:3001`, and Aspire Dashboard is on `http://localhost:18888`.
If you start the API from VS Code using `.NET API + Aspire Dashboard`, the dashboard UI is `http://localhost:18888`.
`http://localhost:8080` is for the API container when the API itself runs inside Docker.

## What This Is

This template uses a single observability model based on `OpenTelemetry`.

That means:

- the API emits traces, metrics, and correlated logs
- telemetry leaves the application over `OTLP`
- the destination can change without changing business code
- local development can use `.NET Aspire Dashboard`
- shared dev and production-like environments can use `Grafana Alloy + Loki + Tempo + Prometheus + Grafana`

The design goal is simple:

- instrumentation lives in the application
- routing and storage live outside the application
- observability stays a side product of the system, not a concern spread across all features

## Core Terms

### OpenTelemetry

`OpenTelemetry` is the instrumentation standard used by the API.

It defines how the application emits:

- `traces` for request and operation flow
- `metrics` for counters, histograms, and gauges
- `logs` that can be correlated with traces

### OTLP

`OTLP` is the protocol used to export telemetry out of the application.

In this project, the API sends data to:

- `.NET Aspire Dashboard` in local dev
- or `Grafana Alloy` in the full stack

### Grafana Alloy

`Grafana Alloy` is the collector/gateway in the full stack.

It receives OTLP telemetry from the API and forwards it to:

- `Tempo` for traces
- `Loki` for logs
- `Prometheus` for metrics

### Grafana LGTM

`LGTM` in this repo means:

- `Loki` for logs
- `Grafana` for dashboards and exploration
- `Tempo` for traces
- `Prometheus` for metrics

This is the operational stack. Aspire Dashboard is the developer-facing shortcut.

## Architecture

### High-level architecture

```text
                            Local Dev Option
API -> OpenTelemetry -> OTLP -> Aspire Dashboard

                            Full Stack Option
API -> OpenTelemetry -> OTLP -> Grafana Alloy
                                        |-> Tempo
                                        |-> Loki
                                        |-> Prometheus
                                                |
                                                v
                                             Grafana
```

### Full-stack architecture in this repo

```text
ASP.NET Core API
    |
    | OTLP (gRPC/HTTP)
    v
Grafana Alloy
    |
    | traces -----------------> Tempo
    | logs -------------------> Loki
    | metrics ----------------> Prometheus remote-write
    |
    v
Grafana
```

### Application architecture

Telemetry registration is intentionally centralized:

- [ObservabilityServiceCollectionExtensions.cs](../src/APITemplate/Extensions/ObservabilityServiceCollectionExtensions.cs)
- [LoggingExtensions.cs](../src/APITemplate/Extensions/LoggingExtensions.cs)

Project-specific telemetry helpers are isolated under:

- [Infrastructure/Observability](../src/APITemplate/Infrastructure/Observability)

This keeps controllers, services, filters, auth handlers, and startup code readable.

## What Is Running

### Application-side telemetry

The API emits:

- inbound HTTP traces and metrics
- outbound `HttpClient` traces and metrics
- PostgreSQL traces via `Npgsql`
- DragonFly/Redis traces via `StackExchangeRedis`
- MongoDB traces via driver diagnostic sources
- GraphQL traces via Hot Chocolate
- runtime and process metrics
- correlated logs with trace/span ids

### Project-specific telemetry

The project adds telemetry for behavior that framework packages do not provide directly:

- startup steps
- Keycloak readiness
- auth/BFF failures
- output cache invalidation
- output cache outcomes
- validation failures
- handled exceptions
- concurrency conflicts
- domain conflicts
- explicit stored procedure spans

## What Is Instrumented

### Built-in instrumentation packages

The application uses OpenTelemetry-compatible packages for:

- `AspNetCore`
- `HttpClient`
- `Runtime`
- `Process`
- `Npgsql`
- `StackExchangeRedis`
- `HotChocolate`
- MongoDB diagnostic sources

### Startup instrumentation

Startup telemetry traces these steps:

- relational migrations
- auth bootstrap seeding
- MongoDB migrations
- Keycloak readiness retries

Relevant code:

- [ApplicationBuilderExtensions.cs](../src/APITemplate/Extensions/ApplicationBuilderExtensions.cs)
- [StartupTelemetry.cs](../src/APITemplate/Infrastructure/Observability/StartupTelemetry.cs)

### Auth and BFF instrumentation

Failure-only telemetry is recorded for:

- missing tenant claim
- unauthorized redirect converted to `401`
- missing refresh token
- token endpoint rejection
- token refresh exception
- cookie refresh failure

Relevant code:

- [AuthenticationServiceCollectionExtensions.cs](../src/APITemplate/Extensions/AuthenticationServiceCollectionExtensions.cs)
- [TenantClaimValidator.cs](../src/APITemplate/Infrastructure/Security/TenantClaimValidator.cs)
- [CookieSessionRefresher.cs](../src/APITemplate/Infrastructure/Security/CookieSessionRefresher.cs)
- [AuthTelemetry.cs](../src/APITemplate/Infrastructure/Observability/AuthTelemetry.cs)

### Cache instrumentation

Output cache telemetry includes:

- invalidation count
- invalidation duration
- cache outcome counter with:
  - `hit`
  - `store`
  - `bypass`

Relevant code:

- [TenantAwareOutputCachePolicy.cs](../src/APITemplate/Api/Cache/TenantAwareOutputCachePolicy.cs)
- [OutputCacheInvalidationService.cs](../src/APITemplate/Api/Cache/OutputCacheInvalidationService.cs)
- [CacheTelemetry.cs](../src/APITemplate/Infrastructure/Observability/CacheTelemetry.cs)

### Validation and exception instrumentation

The API records:

- request rejections by validation
- individual validation errors
- handled exception count
- optimistic concurrency conflicts
- domain conflicts

Relevant code:

- [FluentValidationActionFilter.cs](../src/APITemplate/Api/Filters/FluentValidationActionFilter.cs)
- [ApiExceptionHandler.cs](../src/APITemplate/Api/ExceptionHandling/ApiExceptionHandler.cs)
- [ValidationTelemetry.cs](../src/APITemplate/Infrastructure/Observability/ValidationTelemetry.cs)
- [ConflictTelemetry.cs](../src/APITemplate/Infrastructure/Observability/ConflictTelemetry.cs)

### Stored procedure instrumentation

Stored procedures get explicit parent application spans on top of provider-level `Npgsql` spans.

Relevant code:

- [StoredProcedureExecutor.cs](../src/APITemplate/Infrastructure/StoredProcedures/StoredProcedureExecutor.cs)
- [StoredProcedureTelemetry.cs](../src/APITemplate/Infrastructure/Observability/StoredProcedureTelemetry.cs)

## How the API Connects to Observability

## Application configuration

Observability settings live in [appsettings.json](../src/APITemplate/appsettings.json):

```json
{
  "Observability": {
    "ServiceName": "APITemplate",
    "Otlp": {
      "Endpoint": "http://localhost:4317"
    },
    "Aspire": {
      "Endpoint": "http://localhost:4317"
    },
    "Exporters": {
      "Aspire": {
        "Enabled": null
      },
      "Otlp": {
        "Enabled": false
      },
      "Console": {
        "Enabled": false
      }
    }
  }
}
```

Supported keys:

| Key                                       | What it does                                 |
| ----------------------------------------- | -------------------------------------------- |
| `Observability:ServiceName`               | Service name attached to telemetry resources |
| `Observability:Otlp:Endpoint`             | OTLP collector endpoint, usually Alloy       |
| `Observability:Aspire:Endpoint`           | OTLP endpoint for Aspire Dashboard           |
| `Observability:Exporters:Aspire:Enabled`  | Force Aspire on/off                          |
| `Observability:Exporters:Otlp:Enabled`    | Force OTLP exporter on/off                   |
| `Observability:Exporters:Console:Enabled` | Enable OpenTelemetry console export          |

### Exporter behavior

Current default behavior is:

- local non-container development:
  - Aspire exporter enabled
  - OTLP exporter disabled unless explicitly turned on
- containerized environments:
  - OTLP exporter enabled
  - Aspire exporter disabled

This logic lives in:

- [ObservabilityServiceCollectionExtensions.cs](../src/APITemplate/Extensions/ObservabilityServiceCollectionExtensions.cs)

### Environment variable examples

Run local API and send telemetry to Alloy:

```powershell
$env:Observability__Otlp__Endpoint="http://localhost:4317"
$env:Observability__Exporters__Otlp__Enabled="true"
$env:Observability__Exporters__Aspire__Enabled="false"
dotnet run --project src/APITemplate
```

Run local API and send telemetry only to Aspire Dashboard:

```powershell
$env:Observability__Aspire__Endpoint="http://localhost:4317"
$env:Observability__Exporters__Aspire__Enabled="true"
$env:Observability__Exporters__Otlp__Enabled="false"
dotnet run --project src/APITemplate
```

Enable console exporter for debugging:

```powershell
$env:Observability__Exporters__Console__Enabled="true"
dotnet run --project src/APITemplate
```

## How to Run It

### Option 1: API locally + Aspire Dashboard

Use this when you want quick inspection without the full Grafana stack.

Start Aspire Dashboard:

```bash
docker compose --profile aspire up -d aspire-dashboard
```

Then run the API locally:

```bash
dotnet run --project src/APITemplate
```

Default endpoints:

- Aspire Dashboard UI: `http://localhost:18888`
- Aspire OTLP gRPC exposed on host: `http://localhost:4317`
- Aspire OTLP HTTP exposed on host: `http://localhost:4318`

Flow:

```text
Local API -> localhost:4317 -> Aspire Dashboard
```

### Option 2: API locally + observability stack without Aspire

Use this when you want realistic operational observability while still debugging the API locally.

Start the stack:

```bash
docker compose up -d alloy prometheus loki tempo grafana
```

Then run the API locally and point OTLP to Alloy:

```powershell
$env:Observability__Otlp__Endpoint="http://localhost:4317"
$env:Observability__Exporters__Otlp__Enabled="true"
$env:Observability__Exporters__Aspire__Enabled="false"
dotnet run --project src/APITemplate
```

Flow:

```text
Local API -> localhost:4317 -> Alloy -> Tempo/Loki/Prometheus -> Grafana
```

### Option 3: API locally + full observability stack with Aspire and Grafana

Use this when you want both the LGTM stack and Aspire Dashboard running together.

Start the stack:

```bash
ASPIRE_OTLP_GRPC_PORT=18889 ASPIRE_OTLP_HTTP_PORT=18890 docker compose --profile aspire up -d postgres mongodb keycloak-db keycloak dragonfly alloy prometheus loki tempo grafana aspire-dashboard
```

Then run the API locally and send telemetry to both backends:

```powershell
$env:Observability__Aspire__Endpoint="http://localhost:18889"
$env:Observability__Otlp__Endpoint="http://localhost:4317"
$env:Observability__Exporters__Aspire__Enabled="true"
$env:Observability__Exporters__Otlp__Enabled="true"
dotnet run --project src/APITemplate
```

Default endpoints:

- Grafana UI: `http://localhost:3001`
- Aspire Dashboard UI: `http://localhost:18888`
- Alloy OTLP gRPC exposed on host: `http://localhost:4317`
- Alloy OTLP HTTP exposed on host: `http://localhost:4318`
- Aspire OTLP gRPC exposed on host: `http://localhost:18889`
- Aspire OTLP HTTP exposed on host: `http://localhost:18890`

Flow:

```text
Local API -> localhost:4317 -> Alloy -> Tempo/Loki/Prometheus -> Grafana
         -> localhost:18889 -> Aspire Dashboard
```

### Option 4: full Docker environment

Use this when you want everything in containers, including the API.

Start the whole environment:

```bash
docker compose up -d --build
```

In this mode the API container already has the required env vars:

```yaml
Observability__Otlp__Endpoint: "http://alloy:4317"
Observability__Exporters__Otlp__Enabled: "true"
Observability__Exporters__Aspire__Enabled: "false"
```

That wiring is in [docker-compose.yml](../docker-compose.yml).

### Option 5: production-like Compose

Use the production-like stack without Aspire:

```bash
docker compose -f docker-compose.production.yml up -d --build
```

This uses:

- production environment
- OTLP export to Alloy
- the same LGTM backend pattern

See [docker-compose.production.yml](../docker-compose.production.yml).

## Docker Services and Ports

### Development compose

The default Compose file starts these observability services:

| Service            | Container purpose                  | Host port                                                                      |
| ------------------ | ---------------------------------- | ------------------------------------------------------------------------------ |
| `alloy`            | OTLP receiver and telemetry router | `4317`, `4318`, `12345`                                                        |
| `prometheus`       | metrics backend                    | `9090`                                                                         |
| `loki`             | logs backend                       | `3100`                                                                         |
| `tempo`            | traces backend                     | `3200`                                                                         |
| `grafana`          | dashboards and exploration         | `3001`                                                                         |
| `aspire-dashboard` | optional local telemetry dashboard | `18888`, host `4317`, host `4318` by default, or `18889`, `18890` in full mode |

Important detail:

- `alloy` and `aspire-dashboard` both want OTLP ports on the host
- in full mode the same `aspire-dashboard` service is started with host ports `18889` and `18890` to avoid that conflict
- the provided VS Code launch profiles already separate these modes for you

### Useful URLs

| Tool                                   | URL                            |
| -------------------------------------- | ------------------------------ |
| API (VS Code local launch)             | `http://localhost:5174`        |
| Grafana                                | `http://localhost:3001`        |
| Prometheus                             | `http://localhost:9090`        |
| Loki                                   | `http://localhost:3100`        |
| Tempo                                  | `http://localhost:3200`        |
| Aspire Dashboard                       | `http://localhost:18888`       |
| Health endpoint (VS Code local launch) | `http://localhost:5174/health` |

If the API runs as a container instead of a local VS Code process, use `http://localhost:8080` and `http://localhost:8080/health`.

## How the Full Stack Is Connected

### Alloy

Alloy configuration lives in [config.alloy](../infrastructure/observability/alloy/config.alloy).

What it does:

1. receives OTLP on:
   - `0.0.0.0:4317` for gRPC
   - `0.0.0.0:4318` for HTTP
2. forwards:
   - traces to Tempo
   - logs to Loki
   - metrics to Prometheus remote write
3. exposes its own metrics on `12345` for Prometheus scraping

Logs are sent to Loki over its native `OTLP HTTP` ingest endpoint at `/otlp`, not through the legacy Loki exporter format.
That matters because Grafana `Logs Drilldown` expects Loki's OpenTelemetry-aware label and metadata model.

### Tempo

Tempo stores distributed traces.

In this setup:

- Alloy forwards traces to `tempo:4317`
- Grafana queries Tempo on `http://tempo:3200`

### Loki

Loki stores logs.

In this setup:

- Alloy forwards logs to Loki native OTLP ingest on `http://loki:3100/otlp`
- Grafana queries Loki on `http://loki:3100`

### Prometheus

Prometheus stores metrics.

In this setup:

- Alloy remote-writes metrics to `http://prometheus:9090/api/v1/write`
- Prometheus also scrapes internal targets like Alloy, Loki, and Tempo

Prometheus configuration lives in:

- [prometheus.yml](../infrastructure/observability/prometheus/prometheus.yml)

## Grafana

### Default provisioning

Grafana is provisioned from repository files. No manual datasource setup is required.

Provisioning paths:

- [grafana/provisioning/datasources](../infrastructure/observability/grafana/provisioning/datasources)
- [grafana/provisioning/dashboards](../infrastructure/observability/grafana/provisioning/dashboards)
- [grafana/dashboards](../infrastructure/observability/grafana/dashboards)

### Datasources

Provisioned datasources:

- `Prometheus`
- `Loki`
- `Tempo`

Datasource provisioning file:

- [datasources.yml](../infrastructure/observability/grafana/provisioning/datasources/datasources.yml)

### Grafana credentials

Default dev credentials:

- user: `admin`
- password: `admin`

They can be overridden with:

- `GRAFANA_ADMIN_USER`
- `GRAFANA_ADMIN_PASSWORD`

### What you can do in Grafana

From Grafana you can:

- query metrics in Prometheus
- inspect logs in Loki
- inspect traces in Tempo
- jump from trace to logs using configured trace-to-log links

## VS Code Launch Profiles

This repo includes VS Code profiles for observability workflows:

- `.NET API + Aspire Dashboard`
- `.NET API + Observability`
- `.NET API + Full Observability`

These profiles:

- start required support services first
- run the API locally under the debugger
- keep the API outside Docker so local debugging stays simple

Profile mapping:

- `.NET API + Aspire Dashboard` starts `aspire-dashboard`, so use `http://localhost:18888`
- `.NET API + Observability` starts `alloy`, `grafana`, `tempo`, `loki`, and `prometheus`, so use `http://localhost:3001`
- `.NET API + Full Observability` starts the LGTM stack and `aspire-dashboard`, so use `http://localhost:3001` and `http://localhost:18888`

Use them when you want the easiest developer workflow.

## How to Use It Day to Day

### Typical development flow

For simple local debugging:

1. start Aspire Dashboard
2. run the API locally
3. hit an endpoint
4. inspect traces, logs, and metrics in Aspire

For realistic end-to-end validation:

1. start the full LGTM stack
2. run the API locally or in Docker
3. hit REST and GraphQL endpoints
4. inspect traces in Tempo
5. inspect logs in Loki
6. inspect metrics and dashboards in Grafana

### Example verification flow

Use any endpoint, for example:

- `GET /health`
- `GET /api/v1/Products`
- `GET /graphql`

Then verify:

1. a trace exists for the request
2. child spans exist for database/cache/http calls when applicable
3. logs have `traceId` and `spanId`
4. request metrics appear in Grafana/Prometheus
5. custom metrics appear when relevant:
   - validation errors
   - auth failures
   - cache outcomes
   - conflict counters

## Example Data Paths

### REST request

```text
HTTP GET /api/v1/Products
  -> AspNetCore server span
  -> service/repository work
  -> Npgsql span(s)
  -> Redis span(s) if cache used
  -> request metrics
  -> correlated logs
```

### GraphQL request

```text
POST /graphql
  -> AspNetCore span
  -> HotChocolate request/resolver spans
  -> GraphQL metrics
  -> Npgsql / Mongo / Redis child spans as needed
  -> correlated logs
```

### Startup

```text
Application startup
  -> startup.migrate (postgresql)
  -> startup.seed-auth-bootstrap
  -> startup.migrate (mongodb)
  -> startup.wait-keycloak-ready
```

## How Logs, Traces, and Metrics Correlate

The project uses `Serilog` for application logging and enriches logs with OpenTelemetry context.

That gives you:

- `traceId`
- `spanId`
- request correlation id

This makes it possible to:

- start from a slow trace and find related logs
- start from an error log and find the corresponding trace
- compare traces with metrics spikes

Relevant code:

- [LoggingExtensions.cs](../src/APITemplate/Extensions/LoggingExtensions.cs)
- [ActivityTraceEnricher.cs](../src/APITemplate/Infrastructure/Logging/ActivityTraceEnricher.cs)
- [RequestContextMiddleware.cs](../src/APITemplate/Api/Middleware/RequestContextMiddleware.cs)

## Troubleshooting

### No telemetry visible

Check:

1. exporter flags are correct
2. the endpoint is correct
3. the receiver is listening on the expected host/port
4. the API is actually producing requests

Useful checks:

```bash
docker compose ps
docker compose logs alloy
docker compose logs grafana
docker compose logs aspire-dashboard
```

### Aspire and Alloy both want port `4317`

This is expected.

Use one of these modes:

- Aspire mode
- observability mode without Aspire
- full observability mode with remapped Aspire OTLP on `18889` and `18890`

The launch profiles already separate these scenarios.

### Traces appear but no logs

Check:

- Alloy is forwarding logs to Loki
- Loki is healthy
- Grafana datasource `Loki` is provisioned

If logs appear in dashboards or Explore but not in `Logs Drilldown`, check that Alloy is exporting logs to Loki via native OTLP ingest.
The legacy `otelcol.exporter.loki` path can still show logs in normal Loki queries, but Drilldown can miss them because the OTLP resource labels and structured metadata are not exposed the same way.

### Metrics appear but no application service in dashboards

Check:

- `Observability:ServiceName`
- resource attributes from OTel registration
- Grafana dashboard query filters

### Duplicate DB spans

This project intentionally avoids `EntityFrameworkCore` tracing because provider-level `Npgsql` tracing is already enabled.

That avoids duplicate spans for the same PostgreSQL command.

## Design Decisions

### Why OpenTelemetry everywhere

Because it keeps instrumentation stable and backend choice flexible.

The app does not care whether telemetry ends up in:

- Aspire Dashboard
- Grafana LGTM
- another OTLP-capable collector

### Why Alloy instead of putting exporters everywhere

Because the application should export once.

Alloy then becomes the place where you:

- route telemetry
- enrich or transform telemetry
- switch backends later

### Why Prometheus and not Mimir

Because this template is optimized for simplicity first.

`Prometheus` is enough for:

- local development
- small shared environments
- template-level operational baselines

`Mimir` can be introduced later when scale or retention requires it.

### Why Npgsql tracing and not EF Core tracing

Because provider-level PostgreSQL spans are the most useful signal for this project and avoid duplicate DB spans.

## Relevant Files

### Application

- [Program.cs](../src/APITemplate/Program.cs)
- [ObservabilityServiceCollectionExtensions.cs](../src/APITemplate/Extensions/ObservabilityServiceCollectionExtensions.cs)
- [LoggingExtensions.cs](../src/APITemplate/Extensions/LoggingExtensions.cs)
- [ObservabilityOptions.cs](../src/APITemplate/Application/Common/Options/ObservabilityOptions.cs)
- [appsettings.json](../src/APITemplate/appsettings.json)
- [appsettings.Production.json](../src/APITemplate/appsettings.Production.json)

### Infrastructure

- [docker-compose.yml](../docker-compose.yml)
- [docker-compose.production.yml](../docker-compose.production.yml)
- [config.alloy](../infrastructure/observability/alloy/config.alloy)
- [prometheus.yml](../infrastructure/observability/prometheus/prometheus.yml)
- [datasources.yml](../infrastructure/observability/grafana/provisioning/datasources/datasources.yml)

## Summary

If you want the shortest mental model, it is this:

- the API emits telemetry with OpenTelemetry
- OTLP is the wire protocol
- Aspire Dashboard is the quick local viewer
- Alloy is the collector/router
- Tempo stores traces
- Loki stores logs
- Prometheus stores metrics
- Grafana is where you explore everything together

