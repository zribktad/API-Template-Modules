# APITemplate Monolith — Project Context

Welcome to the **APITemplate** project. This file provides foundational context for navigating, building, and contributing to this modular monolith.

---

## 🏗 Project Overview
A scalable **Modular Monolith** built on **.NET 10**, combining **Clean Architecture** principles with modern tooling. It bridges the gap between development speed and long-term maintainability by isolating feature modules within a single repository.

- **Primary Stack:** .NET 10, PostgreSQL (EF Core), MongoDB, DragonFly (Redis), Keycloak (OIDC/BFF), HotChocolate (GraphQL), WolverineFx (CQRS/Messaging).
- **Key Patterns:** Modular Monolith, CQRS, Repository/Unit of Work, Specification Pattern, Result Pattern (`ErrorOr`), Multi-tenancy, Soft Delete, Auditing.

---

## 🛠 Core Technologies
- **Runtime:** .NET 10.0
- **Persistence:** 
  - **Relational:** PostgreSQL (EF Core 10)
  - **Document:** MongoDB (Polymorphic models)
- **Messaging/CQRS:** WolverineFx (Durable outbox/inbox, mediated handlers)
- **Cache/Session:** DragonFly (Redis-compatible)
- **GraphQL:** HotChocolate (DataLoaders, Mutations)
- **Auth:** Keycloak (Dual auth: JWT Bearer + BFF Cookie session)
- **Background Jobs:** TickerQ (Recurring scheduled tasks)
- **Observability:** OpenTelemetry (Grafana, Loki, Tempo, Prometheus), Serilog
- **Testing:** xUnit v3, Shouldly, Moq, Testcontainers (Postgres/Mongo)

---

## 📂 Project Structure
```text
src/
├── APITemplate/Api/      # Host entry point (DI root, middleware, Program.cs)
├── SharedKernel/         # Shared domain primitives and technical utilities (NOT a module)
├── Contracts/            # Inter-module typed interfaces and message records
└── Modules/
    ├── Identity/         # Auth, BFF, users, roles, Keycloak sync
    ├── ProductCatalog/   # Products, categories, polymorphic media metadata, GraphQL
    ├── Reviews/          # Product reviews, rating aggregation
    ├── Notifications/    # SMTP email pipeline, failed email retry
    ├── BackgroundJobs/   # TickerQ recurring jobs (cleanup, reindex, sync)
    ├── FileStorage/      # Multipart file upload/download
    ├── Webhooks/         # Outbound webhook delivery
    └── Chatting/         # SSE push notifications
```

### Module Internal Structure (Clean Architecture)
Each module follows a consistent internal organization:
- `Contracts/`: Interfaces + message types exposed to other modules.
- `Domain/`: Entities, value objects, domain exceptions.
- `Features/`: Vertical slices: Commands, Queries, Handlers (Wolverine).
- `Persistence/`: DbContext, EF configurations, migrations.
- `Repositories/`: IRepository implementations.
- `Services/`: Domain services, infrastructure adapters.

---

## 📐 Architectural Mandates

### 1. SOLID & Clean Architecture
- **Dependency Rule:** Inner layers (Domain) MUST NOT depend on outer layers. Everything depends on abstractions.
- **Isolation:** Modules MUST NOT reference each other's internal types. Communication happens ONLY via `Contracts` or `SharedKernel.Contracts.Events/Commands`.
- **SRP:** Handlers focus on orchestration. Complex domain logic stays in Entities or Domain Services.

### 2. CQRS & Messaging (WolverineFx)
- Use **Wolverine compound handlers** (`LoadAsync` -> `HandleAsync`) for clean validation/mutation pipelines.
- Controllers/Resolvers MUST NOT call repositories directly; they dispatch commands/queries via `IMessageBus`.
- Cross-module communication:
  - **Events** (`PublishAsync`): Fire-and-forget domain notifications.
  - **Commands** (`InvokeAsync`/`SendAsync`): Targeted cross-module requests.

### 3. Data Integrity & Persistence
- **Repository + Unit of Work:** Use `IUnitOfWork` for transactional boundaries.
- **Soft Delete:** Entities implementing `ISoftDeletable` are automatically filtered and soft-deleted.
- **Multi-Tenancy:** Entities implementing `ITenantEntity` are automatically isolated via `TenantId` global query filters.
- **Auditing:** `AuditInfo` is stamped automatically on all `IAuditableEntity` types.

### 4. Error Handling
- Prefer **Result Pattern** (`ErrorOr<T>`) over throwing business exceptions for control flow.
- Centralized exception mapping in `ApiExceptionHandler` (RFC 7807 `ProblemDetails`).

---

## 🚀 Development Workflow

### Building & Running
```powershell
# Start infrastructure (Postgres, Mongo, Keycloak, DragonFly)
docker-compose up -d

# Run the API
dotnet run --project src/APITemplate/Api

# Health Check
curl http://localhost:5174/health
```

### Testing
- **Unit Tests:** `tests/APITemplate.Tests/Unit/`
- **Integration Tests:** `tests/APITemplate.Tests/Integration/` (uses `WebApplicationFactory` + In-memory/Testcontainers).
- **Run Tests:** `dotnet test`

### Migrations
- **EF Core:** Automatic at startup in Dev. Manual: `dotnet ef migrations add <Name> -p src/Modules/<Module> -s src/APITemplate/Api`.
- **MongoDB:** Automatic at startup using `Kot.MongoDB.Migrations`.

---

## 💡 Project Know-How & Best Practices

### 1. Reusing Infrastructure (Don't Reinvent!)
Before creating a new utility or service, check `SharedKernel` or existing modules for these established patterns:
- **Identity Context:** 
  - `IActorProvider`: Get the current User ID (GUID).
  - `ITenantProvider`: Get the current Tenant ID (GUID).
  - `ICurrentUserContext`: Combined access to user, tenant, and roles.
- **Persistence:**
  - `IUnitOfWork<TContext>`: Use for multi-repository transactions.
  - `IRepository<T>`: Base repository with specification support.
  - `ISoftDeleteCleanupStrategy`: Implement for automatic background cleanup of soft-deleted records.
- **Time & Resilience:**
  - `TimeProvider`: **ALWAYS** use `TimeProvider` instead of `DateTime.UtcNow` to ensure testability via `FakeTimeProvider`.
  - `ResiliencePipelineProvider`: Access pre-configured Polly strategies (retry, circuit breaker).
- **Messaging:**
  - `IMessageBus`: The central nervous system. Use for all command/query dispatching and domain events.
- **Adapters & Markers:**
  - `*RuntimeBridge.cs`: Check module roots for DI wiring adapters.
  - `*DbMarker.cs`: Check module roots for empty marker types used by EF Core design-time factories.

### 2. Module-Specific Know-How
- **Identity:** 
  - Uses nested sub-modules (**Auth/** and **Directory/**). This is a unique pattern; do not add a third sub-module.
  - `Auth` and `Directory` are separate bounded contexts; **NEVER** reference entities between them. Communication is via Wolverine `bus.InvokeAsync`.
  - **Two-Tier Cache:** Uses L1 (Memory) and L2 (Redis) for sessions. Coherence is maintained via Redis Pub/Sub revocation broadcast.
  - **Refresh Coordination:** Redis distributed locks prevent "refresh floods" by ensuring only one instance refreshes tokens at a time.
  - **Normalization:** ALL email/identity lookups MUST use `NormalizedString.Normalize(email)` to prevent duplicate account bugs.
- **ProductCatalog:**
  - Entities live in `Entities/` at the module root, **NOT** in `Domain/`.
  - Beware of naming collision: `Configurations/` (EF mappings) vs `Configuration/` (IOptions).
  - **Dual Database:** Uses `ProductCatalogDbContext` (Postgres) and `MongoDbContext` (MongoDB). Orchestrated via `ProductDataLinks`.
  - **GraphQL:** DataLoaders (e.g., `ProductReviewsByProductDataLoader`) MUST use `IMessageBus` to fetch data, not repositories directly.
- **BackgroundJobs & Notifications:**
  - **Leader Election:** TickerQ uses Redis locks. Default is **Fail-Open** (jobs run if Redis is down), which may cause duplicates.
  - **Email Durability:** `EmailRetryService` commits to the DB **after every single SMTP send** using `CancellationToken.None` to guarantee progress recording even during cancellation.
  - **Automatic Cleanup:** Uses reflection to find `ISoftDeletable` entities and pairs them with `SoftDeleteCleanupStrategy<T>` automatically.
- **SharedKernel:**
  - Purely cross-cutting infrastructure. **NEVER** put module-specific logic or concrete domain entities here.

### 2. Mandatory Test Categorization & Gating
Tests MUST have a `Category` trait. The CI/CD pipeline filters tests by these categories.

| Category | CI/CD Pipeline Stage | Description |
|----------|-----------------------|-------------|
| `Unit` | **Unit Tests** | Fast, isolated logic tests. |
| `Unit.Component` | (Filtered out) | Heavier unit tests involving multiple components/state. |
| `Integration` | **Integration Tests** | Full host + In-memory DB round-trips. |
| `Smoke` | **Smoke Tests** | Critical path verification (Subset of Integration). |
| `Integration.Postgres`| (Manual/Docker) | Tests requiring real Postgres via Testcontainers. |
| `Integration.Docker` | (Manual/Docker) | Tests requiring full Docker infra (Redis, Keycloak, etc). |

**Example:**
```csharp
[Trait("Category", "Unit")]
public class MyServiceTests { ... }
```

**Gating Rule:** Integration tests are excluded from compilation by default. You MUST explicitly add them to the `.csproj` using `<Compile Include="..." />` to run or test them.

### 3. Cross-Module Communication (WolverineFx)
Modules MUST NOT reference each other's internal projects. All communication is mediated by `IMessageBus`:

- **Events (`PublishAsync`):** Fire-and-forget notifications. Use when a module wants to announce a change (e.g., `TenantSoftDeletedNotification`) without caring who listens.
- **Commands (`InvokeAsync`):** Request/Response. Use when you need a result from another module (e.g., `ValidateProductExistsQuery`).
- **Commands (`SendAsync`):** Fire-and-forget task dispatch. Use for background-style tasks (e.g., `SendWebhookCallbackCommand`).

**Rule:** Inter-module messages MUST be defined in `SharedKernel/Contracts/`.

---

## 📜 Project Conventions & Style

1. **Explicit Typing:** Declare full types unless the RHS is trivially obvious (e.g., `new()`, literals). Avoid `var` for complex inferred types. `.editorconfig` will trigger warnings.
2. **Naming:** `PascalCase` for classes/methods, `_camelCase` with underscore for private fields.
3. **Braces & Namespaces:** Use **Allman braces** (new line) and **file-scoped namespaces** (`namespace Foo;`).
4. **Wolverine Handlers:** MUST end with `Handler`. Prefer `static HandleAsync` methods with parameter-injected dependencies.
5. **Controllers:** MUST inherit `ApiControllerBase` and inject ONLY `IMessageBus`. Never call repositories or services directly.
6. **Logging:** Use source-generated `LoggerMessage` partial methods for performance.
7. **Build Integrity:** `TreatWarningsAsErrors` is ENABLED. All compiler warnings will break the build.
8. **Solution Format:** Uses `.slnx` (XML format). Requires .NET 10 SDK.
9. **White-Box Testing:** Every project has `InternalsVisibleTo("APITemplate.Tests")`.

---

## 🔗 Key Documentation
- [REST Endpoint Guide](docs/rest-endpoint.md)
- [GraphQL Endpoint Guide](docs/graphql-endpoint.md)
- [Authentication Workflow](docs/AUTHENTICATION.md)
- [Testing Guide](docs/testing.md)
- [Architecture Analysis](ARCHITECTURE_ANALYSIS.md)
