# Code Analysis Report — API-Template-Monolith

## Scope

| | |
|---|---|
| **Target** | `API-Template-Monolith` — all modules |
| **Concern** | Architectural simplification and maintainability |
| **Agents** | Structure, Patterns & SOLID, Data Flow |
| **Pattern** | Modular Monolith · Wolverine CQRS · Clean Architecture |

---

## Findings

### Strengths

- **Clean modularity**: each module has its own registration, DI scope, and boundaries — `ProductCatalogModule.cs`, `NotificationsModule.cs`
- **Wolverine multi-method handler pattern** (`LoadAsync → HandleAsync`) enables a clean pipeline without DI factory bloat — `AssignUserRolesCommandHandler.cs:11-66`
- **Sophisticated UnitOfWork** with savepoint management and state tracking — `UnitOfWork.cs:104-133`
- **Compiled expression tree projections** instead of AutoMapper — `UserMappings.cs:8-24`
- **Centralized error handler** with metrics — `ApiExceptionHandler.cs:6-122`
- **Correlation ID** flows through the entire pipeline via Serilog LogContext and response headers — `CorrelationContextMiddleware.cs`
- **Specification pattern** consistently used across 39 specifications — `ProductReviewSpecification.cs` and others

### Issues

| Severity | Issue | Location | Found by |
|----------|-------|----------|----------|
| **HIGH** | `ProductCatalog.csproj` references the `Reviews` project — direct cross-module coupling | `ProductCatalog.csproj:9-10`, `GlobalUsings.cs:15-16`, `ProductReviewMutations.cs:17-28` | Structure, Patterns |
| **MED** | Module registration order enforced only by a comment — no runtime/compile-time protection | `Program.cs:49-51`, `AuthenticationHostingExtensions.cs:17-19` | Data Flow, Structure |
| **MED** | Cache invalidation cascades are statically too broad (ForProductDeletion invalidates Reviews even when no reviews are affected) | `CacheInvalidationCascades.cs:4-29` | Data Flow |
| **MED** | Multi-method handlers — no compile-time guarantee of correct Wolverine pipeline staging | `AssignUserRolesCommandHandler.cs:11-66` | Data Flow |
| **MED** | Event handlers discovered via reflection — orphaned events are silently ignored | `WolverineModuleDiscovery.cs:26-43` | Data Flow |
| **MED** | `KeycloakAdminService.cs` 307 lines — SRP violation | `KeycloakAdminService.cs` | Structure |
| **MED** | `ApiServiceCollectionExtensions.cs` 182 lines — mixes validation, caching, Redis, OpenAPI | `ApiServiceCollectionExtensions.cs:24-109` | Structure |
| **MED** | DRY violation — tenant-scoped filtering logic repeated in every module's specifications | ProductCatalog/Reviews/Identity Specifications | Patterns |
| **LOW** | `IStoredProcedureExecutor` mixes read + write — ISP violation | `IStoredProcedureExecutor.cs:8-51` | Patterns |
| **LOW** | `UnitOfWorkForwarder` is a pure forwarding wrapper with no logic of its own | `UnitOfWorkForwarder.cs` | Patterns |
| **LOW** | Inconsistent internal module structures (ProductCatalog has 17 top-level folders, Chatting only 1) | `src/Modules/*/` | Structure |
| **LOW** | `Configuration/` vs `Configurations/` duplication in ProductCatalog | `src/Modules/ProductCatalog/` | Structure |

### Quick Wins

- Remove `ProductCatalog → Reviews` project reference; move shared DTOs to `SharedKernel.Contracts` (~2–3h)
- Runtime assertion after module registration — verify required DI registrations are present before `app.Build()` (~15 min)
- Merge `Configuration/` and `Configurations/` in ProductCatalog (~30 min)
- Create `TenantScopedSpecification<T>` base class in SharedKernel — eliminate duplication across ~10 specifications (~1.5h)
- Split `ApiServiceCollectionExtensions` into `AddValidationServices`, `AddCachingServices`, `AddOpenApiServices` (~1h)

---

## Solutions

### Solution 1: Targeted Fixes (Minimal)

**What it does:**
Addresses only the most critical issue — cross-module coupling between `ProductCatalog` and `Reviews` — plus a few isolated quick wins. Each change is independent, with no larger structural refactor. `ProductReviewMutations` are moved out of ProductCatalog GraphQL into the Reviews module, or their contract is extracted into `SharedKernel.Contracts`. Other issues (IStoredProcedureExecutor, UnitOfWorkForwarder, module structures) are deferred.

**Concrete implementation:**
- `SharedKernel.Contracts` → add `ICreateProductReviewRequest`, `ProductReviewResponse` DTOs
- `ProductCatalog.csproj` → remove `<ProjectReference Include="..\Reviews\Reviews.csproj" />`
- `GlobalUsings.cs:15-16` → remove `using Reviews.Domain`, `using Reviews.Features`
- `ProductReviewMutations.cs` → wire through SharedKernel contract (not a direct Reviews command)
- `Configuration/` + `Configurations/` → merge into a single folder

**How it solves the identified issues:**
- **Cross-module coupling (HIGH)**: ProductCatalog and Reviews become independent projects again
- **Configuration duplication (LOW)**: cognitive confusion eliminated

**Why different from current:** Only removes one project reference and extracts a shared contract.

**Assumptions / prerequisites:** Reviews module must export commands via bus (Wolverine) or via SharedKernel contract.

---

### Solution 2: Structural Hardening ← Recommended

**What it does:**
Addresses all HIGH and MEDIUM issues systematically without changing the fundamental architecture. Includes: (1) decoupling ProductCatalog → Reviews, (2) runtime registration validation in Program.cs, (3) `TenantScopedSpecification<T>` base class, (4) startup-time Wolverine handler discovery validation, (5) breaking up large classes. This is a maintainability refactor — each change solves a concrete pain point, not a speculative architecture.

**Concrete implementation:**
- `SharedKernel.Contracts` → add shared review DTOs and contract
- `ProductCatalog.csproj` → remove Reviews reference
- `SharedKernel/Specifications/TenantScopedSpecification.cs` → new base class, update ~10 specifications
- `Program.cs` (after `AddModules()`, before `app.Build()`) → add `ValidateModuleRegistrations()` helper
- `WolverineModuleDiscovery.cs` → add startup validation — all `IEvent` types must have at least 1 handler
- `KeycloakAdminService.cs` → split into `KeycloakUserManager`, `KeycloakRoleManager`, `KeycloakTokenProvider`
- `ApiServiceCollectionExtensions.cs` → split into 4 focused extension classes
- `IStoredProcedureExecutor.cs` → split into `IStoredProcedureReader` + `IStoredProcedureWriter`
- Unify internal module structure according to a canonical pattern

**How it solves the identified issues:**
- **Cross-module coupling (HIGH)**: fully resolved
- **Fragile registration order (MED)**: runtime assertion catches errors immediately
- **Orphaned event handlers (MED)**: startup validation catches missing handlers
- **DRY in specifications (MED)**: TenantScopedSpecification eliminates ~10 duplicates
- **SRP violations (MED)**: large classes broken up

**Why different from current:** Adds a safety net for implicit dependencies and eliminates duplication without changing the architecture.

**Assumptions / prerequisites:** Wolverine supports startup-time handler discovery via `IWolverineRuntime` or reflection on registered assemblies.

---

### Solution 3: Event-Driven Decoupling [BREAKING]

**What it does:**
Instead of cross-module DTOs in SharedKernel.Contracts, this takes a full event-driven approach: every cross-module request goes through the Wolverine bus with explicitly typed messages. `ProductReviewMutations` in ProductCatalog GraphQL no longer sends a command directly to Reviews — it publishes `ProductReviewRequestedEvent` on the bus, Reviews handles it asynchronously and replies via `ProductReviewCreatedEvent`. Additionally, static cache invalidation cascades are replaced by domain events (`ProductChangedEvent.Invalidates = [...]`), eliminating the static cascade map.

**Concrete implementation:**
- `SharedKernel.Events` → new event types: `ProductReviewRequestedEvent`, `ProductReviewCreatedEvent`
- `ProductReviewMutations.cs` → publishes `ProductReviewRequestedEvent` on bus, awaits correlation response
- Reviews module → new handler `ProductReviewRequestedEventHandler` → creates review → publishes `ProductReviewCreatedEvent`
- `CacheInvalidationCascades.cs` → replace static collections with domain event subscription registrations
- GraphQL mutations → must be adapted for async response pattern (not fire-and-return-sync)

**How it solves the identified issues:**
- **Cross-module coupling (HIGH)**: direct dependency fully eliminated — modules communicate only via bus
- **Cache cascades (MED)**: each domain event declares what it invalidates, not a static map
- **Module coupling generally**: foundation for true module independence

**Why different from current:** Changes synchronous cross-module calls to an asynchronous event flow — a fundamental change to the API contract for mutations.

**Assumptions / prerequisites:** GraphQL mutations can return an async response (subscription or polling endpoint). Wolverine request/reply pattern (`bus.InvokeAsync<T>`) must be usable for synchronous results.

---

## Comparison

| Criteria | S1: Targeted Fixes | S2: Structural Hardening | S3: Event-Driven [BREAKING] |
|----------|-------------------|--------------------------|------------------------------|
| **Complexity** | Low | Medium | High |
| **Effort** | 4–6 hours | 2–3 days | 1–2 weeks |
| **Risk** | Low | Low | Medium–High |
| **Maintainability** | `+` | `++` | `++` |
| **Scalability** | `0` | `+` | `++` |
| **Breaking changes** | None | Minor | Major (mutations API contract) |
| **Resolves HIGH issue** | Yes | Yes | Yes |
| **Resolves MED issues** | Partially | All | All + more |
| **Key trade-off** | Only a patch, rest remains | Best effort-to-value ratio | Clean architecture, but async mutations are a breaking change |

---

## Recommendation

- **Best choice**: **Solution 2 — Structural Hardening**
- **Reasoning**: Resolves all identified pain points without breaking changes. The project has a healthy foundation — it does not need a revolution, it needs disciplined closure of open issues. In particular, `TenantScopedSpecification<T>` and Wolverine startup validation deliver high ROI at minimal risk.
- **Risks**: Wolverine startup handler discovery requires verifying that the `IWolverineRuntime` API is available in the current version. TenantScopedSpecification must be generically tested before rolling out to all modules.

### Next Steps (in order)

1. **Remove `ProductCatalog → Reviews` project reference** — do this first (blocking build issue if modules are ever separated)
2. **`TenantScopedSpecification<T>`** — high ROI, clean refactor with no risk
3. **Startup registration validation** in `Program.cs` — catches implicit dependencies
4. **Split `KeycloakAdminService`** and **`ApiServiceCollectionExtensions`** — improves testability
