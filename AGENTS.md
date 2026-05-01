# PROJECT KNOWLEDGE BASE

**Generated:** 2026-05-01
**Commit:** fe5d1d5
**Branch:** main

## OVERVIEW
Modular Monolith on .NET 10 — REST + GraphQL API with PostgreSQL (EF Core 10), MongoDB (polymorphic docs), Keycloak OIDC auth, Wolverine CQRS dispatch, TickerQ background jobs. Multi-tenant with soft-delete, audit stamping, and optimistic concurrency.

## STRUCTURE
```
APITemplate.slnx                          # XML-format solution (10 projects)
Directory.Build.props                     # net10.0, nullable, warnings-as-errors
Directory.Packages.props                  # 86 centrally pinned NuGet versions
src/
├── APITemplate/Api/                      # Host — DI composition root, middleware, Program.cs
├── SharedKernel/                         # Domain primitives + app utilities + infra helpers + Contracts
└── Modules/                              # 8 feature modules (no cross-module references except ProductCatalog→Reviews)
    ├── Identity/                         # Auth, BFF sessions, users, roles, Keycloak sync — MAJOR deviation (nested Auth/Directory sub-modules)
    ├── ProductCatalog/                   # Products, categories, GraphQL, MongoDB — MODERATE deviation (Entities/ outside Domain/)
    ├── Reviews/                          # Product reviews, rating aggregation
    ├── Notifications/                    # SMTP email pipeline, failed email retry
    ├── BackgroundJobs/                   # TickerQ recurring jobs, cross-module dispatch
    ├── FileStorage/                      # Multipart file upload/download
    ├── Webhooks/                         # Outbound HTTP callbacks, HMAC signing
    └── Chatting/                         # SSE streaming only (thin module)
tests/
└── APITemplate.Tests/                    # xUnit v3, Moq, Testcontainers, selective Compile Include
infrastructure/                           # K8s manifests, Grafana/Prometheus/Loki/Tempo configs, Keycloak realm JSON
docs/                                     # 16 how-to guides (REST, GraphQL, migrations, auth, testing, etc.)
```

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Add REST endpoint | `src/Modules/<Module>/Features/` | Entity → DTO → FluentValidator → Wolverine handler → controller. See `docs/rest-endpoint.md` |
| Add GraphQL type | `src/Modules/ProductCatalog/GraphQL/` | Type → Query/Mutation → optional DataLoader. See `docs/graphql-endpoint.md` |
| Add EF migration | `src/Modules/<Module>/Persistence/` | `dotnet ef migrations add` per module csproj. See `docs/ef-migration.md` |
| Add MongoDB migration | Module root `Migrations/` folder | Kot.MongoDB.Migrations. See `docs/mongodb-migration.md` |
| Cross-module event | `src/SharedKernel/Contracts/Events/` | PublishAsync for fire-and-forget |
| Cross-module command | `src/SharedKernel/Contracts/Commands/` | InvokeAsync for request/response |
| Add background job | `src/Modules/BackgroundJobs/TickerQ/` | Cron-scheduled, Redis leader election |
| Change auth/session | `src/Modules/Identity/Auth/` | BFF Cookie + JWT dual flow |
| Modify app config | `src/APITemplate/Api/appsettings*.json` | Env vars > Development.json > Production.json baseline |
| CI pipeline | `.github/workflows/pr-validation.yml` | Unit + Smoke + Integration tests. No deploy workflow exists |
| Docker build | `src/APITemplate/Api/Dockerfile` | 3-stage build. See build/CI agent findings for gaps |

## CODE MAP

| Symbol | Type | Location | Role |
|--------|------|----------|------|
| `Program` | partial class | `src/APITemplate/Api/Program.cs` | Sole entry point, DI composition root |
| `AddIdentityModule()` | extension | `IdentityModule.cs` + partials | Registers Auth + Directory sub-modules |
| `AddProductCatalogModule()` | extension | `ProductCatalogModule.cs` | Registers GraphQL, MongoDB, EF Core |
| `AddReviewsModule()` | extension | `ReviewsModule.cs` | Registers review persistence + HTTP |
| `AddNotificationsModule()` | extension | `NotificationsModule.cs` | Registers SMTP pipeline |
| `AddBackgroundJobsModule()` | extension | `BackgroundJobsModule.cs` | Registers TickerQ scheduler |
| `AddWebhooksModule()` | extension | `WebhooksModule.cs` | Registers webhook queue + HMAC |
| `AddFileStorageModule()` | extension | `FileStorageModule.cs` | Registers file streaming |
| `AddChattingModule()` | extension | `ChattingModule.cs` | Registers SSE endpoints |
| `IUnitOfWork` | interface | `SharedKernel/Infrastructure/UnitOfWork/` | Transaction boundary + retry |
| `BaseRepository<T>` | class | `SharedKernel/Infrastructure/Repositories/` | Generic EF CRUD base |
| `ValidationBehavior<T>` | class | `SharedKernel/Application/Validation/` | Wolverine FluentValidation middleware |
| `ICurrentUserContext` | interface | `SharedKernel/Application/Context/` | Resolves user ID + tenant ID from HTTP |
| `ApiControllerBase` | class | `SharedKernel/Contracts/Api/` | Base controller for all modules |

## CONVENTIONS
- **Explicit types everywhere** — `var` discouraged (`.editorconfig` warning). Never `var int x = 5`.
- **File-scoped namespaces** — `namespace Foo;` not `namespace Foo { }`.
- **Allman braces** — opening brace on new line.
- **Private fields**: `_camelCase` with underscore prefix.
- **Using directives**: outside namespace, system directives first.
- **Wolverine handlers**: static `HandleAsync` method, dependencies injected as parameters.
- **Controllers**: inject only `IMessageBus` — never call services/repositories directly.
- **Specifications**: query logic in `Ardalis.Specification` classes, not scattered.
- **Module registration**: `<Module>Module.cs` + optional partial classes (Identity splits into `.Auth.cs` + `.Directory.cs`).
- **Central package management** — no version strings in `.csproj` files. All versions in `Directory.Packages.props`.

## ANTI-PATTERNS (THIS PROJECT)
- **NEVER** reference another module's internal types directly — use `SharedKernel.Contracts` events/commands via `IMessageBus`. (Exception: ProductCatalog→Reviews direct reference is the sole violation.)
- **NEVER** commit real secrets to `appsettings.json` — use env vars or secret manager.
- **NEVER** use string concatenation in `FromSql()` — always interpolated strings for parameterization.
- **NEVER** bypass `IUnitOfWork` for transactional writes — all commits go through it.
- **NEVER** add cross-module project references — modules communicate via Wolverine message bus.
- **ProductCatalog→Reviews**: the one direct module reference. Do not add more.

## UNIQUE STYLES
- **`*RuntimeBridge.cs`** files at module roots (Identity, ProductCatalog, Reviews, Notifications, BackgroundJobs, Webhooks) — DI wiring adapters.
- **`*DbMarker.cs`** files — empty marker types for EF Core design-time factory resolution per module.
- **Selective Compile Include** in tests: integration test files are excluded by default, then explicitly re-included one-by-one in `.csproj`.
- **`InternalsVisibleTo("APITemplate.Tests")`** on every project — white-box testing enabled across the solution.
- **Identity nested sub-modules**: Auth/ and Directory/ each have their own internal Clean Architecture (Entities/, Features/, Handlers/, etc.). This is the only module with this pattern.
- **TreatWarningsAsErrors = true** — all compiler warnings are build-breaking.
- **`.slnx` XML solution format** — not traditional `.sln`. Requires .NET 10 SDK.

## COMMANDS
```bash
# Restore, build, test
dotnet restore APITemplate.slnx
dotnet build --no-restore APITemplate.slnx
dotnet test --no-build APITemplate.slnx

# Fast unit tests (inner loop)
dotnet test APITemplate.slnx --filter "Category=Unit&Category!=Unit.Component"

# Integration tests (in-memory, no external deps)
dotnet test APITemplate.slnx --filter "Category=Integration"

# Testcontainers PostgreSQL tests (requires Docker)
dotnet test APITemplate.slnx --filter "Category=Integration.Postgres"

# Start infrastructure (dev)
docker compose up -d

# Start infra only, run API locally
docker compose up -d postgres mongodb keycloak dragonfly
dotnet run --project src/APITemplate

# Docker build (multi-stage)
docker build -t apitemplate-image:1.0 -f src/APITemplate/Api/Dockerfile .

# EF migration (per-module, example: Identity)
dotnet ef migrations add <Name> --project src/Modules/Identity/Identity.csproj --context IdentityDbContext

# MongoDB migration (auto-runs at startup via Kot.MongoDB.Migrations)
# No manual CLI command — migrations in module Migrations/ folder execute on UseDatabaseAsync()
```

## NOTES
- **No deployment workflow exists** — CI only validates PRs. Docker push and K8s deploy steps described in README are not implemented.
- **Dockerfile runs as root** — no `USER` directive. Security concern for production.
- **Dockerfile lacks HEALTHCHECK** — `/health` endpoint exists but not wired into container image.
- **`.dockerignore` is minimal** — `tests/`, `docs/`, `.github/` are sent to Docker daemon.
- **Known suppressed vulnerability**: GHSA-9mv3-2cwr-p262 (DataProtection 10.0.6) — tracked, no patch available.
- **Identity module is the complexity hotspot** — 83 files, nested Auth/Directory sub-modules, partial class registration.
- **ProductCatalog `Configuration/` vs `Configurations/`** — two similarly-named folders at root. Configuration/ = IOptions, Configurations/ = EF type configs. Easy to confuse.
- **Chatting module is minimal** — only `Features/` folder, no persistence, no domain entities. Acceptable for SSE-only scope.
- **EF Core + MongoDB migrations run automatically at startup** via `UseDatabaseAsync()` in Development — no manual `dotnet ef database update` needed.
