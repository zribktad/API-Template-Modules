# TODO

## Distributed Consistency — Identified Issues

### Critical

- [x] **`CleanupOrphanedProductDataHandler` — `IgnoreQueryFilters()` data leak** — soft-deleted
  `ProductDataLink` rows are included in the "linked IDs" query (`IgnoreQueryFilters()` bypasses the
  soft-delete filter), so MongoDB `ProductData` documents whose only references are soft-deleted links
  are never considered orphaned. After `TenantCascadeDeleteHandler` soft-deletes all
  `ProductDataLinks` for a tenant, the corresponding MongoDB documents are protected from cleanup
  forever. Data accumulates permanently with no eviction path.
  **Fix:** Remove `IgnoreQueryFilters()` from the linked-IDs query in `CleanupOrphanedProductDataHandler`
  so only active links protect documents.

### High Priority

- [x] **`DeleteProductDataCommand` — dual-commit inconsistency** — the SQL transaction (soft-delete
  `ProductDataLinks`) commits atomically, but the subsequent MongoDB `SoftDeleteAsync` call is a
  separate operation protected only by a Polly retry pipeline. If MongoDB remains unreachable after
  all Polly retries, the links are gone while the `ProductData` document stays active. The cleanup
  job cannot recover this because of the `IgnoreQueryFilters` bug above.
  **Fix:** Convert to outbox-first pattern — emit `ProductDataSoftDeletedEvent` in the same SQL
  transaction; a background handler performs the MongoDB soft-delete with full Wolverine retry/dead-letter.

- [x] **`TenantCascadeDeleteHandler` — MongoDB `ProductData` documents never cleaned** — the handler
  cascades soft-delete to `Products`, `Categories`, and `ProductDataLinks` in PostgreSQL but never
  touches MongoDB. All `ProductData` documents belonging to the deleted tenant become permanent
  zombies (compounded by the `IgnoreQueryFilters` bug that prevents the cleanup job from removing them).
  **Fix:** After the SQL transaction, emit a `TenantProductDataCleanupEvent`; a new handler deletes
  all MongoDB `ProductData` documents for the tenant.

- [x] **`DeleteUserCommandHandler` / `SetUserActiveCommandHandler` — Keycloak-first creates reverse
  inconsistency** — both handlers mutate Keycloak before committing the DB change. If the PostgreSQL
  commit fails after Keycloak succeeds, the systems diverge: the user is deleted in Keycloak but
  still exists in the DB (`DeleteUser`), or the enabled/disabled state differs (`SetUserActive`). The
  `catch` block in `DeleteUserCommandHandler` only logs and rethrows — no compensation is attempted.
  **Fix:** Either reverse the order to DB-commit-first (idempotent Keycloak retry on re-run), or
  convert Keycloak calls to the outbox-first pattern used by `ProvisionKeycloakUserHandler`.

- [ ] **`ProvisionKeycloakUserHandler` — zombie user after permanent Keycloak failure** — after
  Wolverine exhausts all retries (`ScheduleRetry 5 s / 30 s / 5 min`) the message moves to the
  dead-letter queue. The `AppUser` record remains in PostgreSQL with `KeycloakUserId = null`; the
  user sees a successful registration but can never log in. No compensating action, no alert.
  **Fix (recommended):** Add `ProvisioningStatus` (`Pending`/`Completed`) to `AppUser`; expose status
  via the admin API. On dead-letter, notify ops via a structured log alert or a
  `UserProvisioningFailedEvent` so the record can be retried or deleted manually via the dead-letter REST API.

### Medium Priority

- [ ] **`CleanupOrphanedProductDataHandler` — race condition** — between the MongoDB page query and
  the PostgreSQL linked-IDs query, a new `ProductDataLink` could be inserted for a document in the
  current page. The handler then deletes a MongoDB document that now has an active reference, leaving
  a dangling foreign key in PostgreSQL.
  **Fix:** Use a MongoDB client session (`IClientSessionHandle`) to make the `Find` + `DeleteMany`
  atomic within a single MongoDB transaction, or add a `PendingDeletion` status to `ProductData`
  documents as a soft-lock before cross-checking links.

- [x] **`KeycloakPasswordResetCommandHandler` — silent Keycloak failure** — all exceptions are caught
  and swallowed; the handler unconditionally returns `Result.Success`. Users receive a success
  response even when the Keycloak action email was never queued or sent.
  **Fix:** Either propagate the error as `ErrorOr<Success>` so the caller can surface a 502/503, or
  emit a `PasswordResetRequestedEvent` via the durable outbox so the Keycloak call is retried
  automatically.

### Resolution Strategy — Layered Defense

All cross-system consistency issues above should be resolved using a **layered defense-in-depth
strategy** combining four complementary patterns. Each layer catches failures that the previous layer
missed:

```
Request → Layer 1: Idempotent Handler
              ↓
          Layer 2: Transactional Outbox (DB + event in one ACID transaction)
              ↓ (background worker)
          Handler → external system (Keycloak / MongoDB)
              ↓ (if all retries exhausted)
          Layer 3: Dead-Letter Compensation (auto-compensate on permanent failure)
              ↓ (if compensation fails, or unknown edge case)
          Layer 4: Reconciliation Job (periodic state comparison & repair)
```

**Layer 1 — Idempotent Handlers** `[FOUNDATION]`
All handlers interacting with external systems must be safely re-runnable. `KeycloakAdminService`
already handles this (409 Conflict on create, 404 on delete). Extend to all MongoDB handlers.

**Layer 2 — Transactional Outbox (DB-first)** `[MUST HAVE]`
Convert ALL cross-system writes to the outbox pattern already used by `ProvisionKeycloakUserHandler`:
write to primary DB + emit event in one ACID transaction; background handler calls the external
system with full Wolverine retry/dead-letter support. Applies to: `DeleteUser`, `SetUserActive`,
`PasswordReset` (Keycloak), `DeleteProductData`, `TenantCascadeDelete` (MongoDB).

**Layer 3 — Dead-Letter Auto-Compensation** `[SHOULD HAVE]`
When a message exhausts all retries and lands in `wolverine_dead_letters`, a compensating handler
automatically reverses the primary DB change. Implementation: either Wolverine's `HandleFailureAsync`
convention per handler, or a centralized `DeadLetterCompensationJob` that maps message types to
compensation commands (e.g., `ProvisionKeycloakUserEvent` → delete orphaned user from DB).

**Layer 4 — Reconciliation Job** `[NICE TO HAVE]`
Periodic background job that compares state between systems and repairs drift regardless of cause.
Fix the existing `CleanupOrphanedProductDataHandler` (`IgnoreQueryFilters` bug). Add a Keycloak
reconciliation job that detects: users with `KeycloakUserId = null` older than threshold, Keycloak
accounts without matching DB records, `IsActive` state mismatches.

**Saga pattern** is deferred until a workflow grows beyond 2 steps (e.g., registration + Keycloak +
tenant assignment + welcome email + subscription). For current 2-step operations, Outbox +
Dead-Letter Compensation provides equivalent guarantees with significantly less overhead.

---

## Architecture Review — Identified Issues

### Critical

- [x] **Authorization code duplication** — `PermissionAuthorizationHandler` and `PermissionPolicyProvider` exist in both
  `APITemplate.Api/Api/Authorization/` and `Identity.Api/Authorization/` with divergent implementations (different
  constructors, auth schemes, `[SensitiveData]` attributes). Move to SharedKernel or Contracts as the single source of
  truth.

### High Priority

- [x] **Mixed error handling patterns** — unified on `ErrorOr<T>` return pattern. Exception-based classes (
  `NotFoundException`, `ConflictException`, `ValidationException`, `AppException` hierarchy) removed.
  `ApiExceptionHandler` is now a safety net for `DbUpdateConcurrencyException` (409) and unhandled exceptions (500)
  only. Validation is centralized via Wolverine middleware and MVC `FluentValidationActionFilter`.
- [x] **Options classes split between SharedKernel and modules** — `BffOptions`, `KeycloakOptions`, `CorsOptions`,
  `EmailOptions`, `SystemIdentityOptions` exist in both places. Module-specific options (`BackgroundJobsOptions`,
  `FileStorageOptions`) are in SharedKernel where they don't belong. Each module should own its options; SharedKernel
  should contain only truly shared types.
- [x] **Anemic domain models** — `Tenant` enriched with `Activate()`/`Deactivate()` and name validation. `StoredFile`
  is intentionally a metadata record (no business logic exists — validation depends on config, not entity state); anemic
  model is correct here.

### Medium Priority

- [x] **Business logic in handlers** — Added `Create()` factory methods to `AppUser`, `Category`, `ProductReview`, and
  `JobExecution`. All handlers updated to use factory methods. `CreateUserCommand` refactored to outbox-reversed order:
  user saved to DB first (KeycloakUserId=null), then `ProvisionKeycloakUserEvent` delivered via Wolverine durable outbox
  to `ProvisionKeycloakUserHandler` which creates the Keycloak account and links it back. Eliminates orphaned Keycloak
  users. `KeycloakAdminService.CreateUserAsync` made idempotent (handles 409 Conflict via username lookup).
- [ ] **Inconsistent logging** — source-generated `[LoggerMessage]` with event IDs is already used across modules, but
  inline `logger.LogWarning()` remains in `TenantClaimValidator` and `CookieSessionRefresher`. Finish migration to
  source-generated logging for these remaining paths.
- [ ] **Incomplete health checks** — Redis/Dragonfly and MongoDB checks are implemented, but Wolverine messaging health
  coverage is still missing. Add remaining messaging-focused checks if required by your ops model.
- [ ] **Soft delete cascade via three mechanisms** — the same business rule (cascade deletes on soft-delete) is
  implemented via database cascade rules, infrastructure `SoftDeleteProcessor`, and Wolverine event handlers
  simultaneously. Consolidate to event-driven approach only.
- [ ] **`ClearCategoryAsync` bypasses EF Core change tracker** — `ExecuteUpdateAsync` is a bulk SQL operation that skips
  the DbContext tracker. If products are tracked in the same session (e.g. loaded during validation), their in-memory
  `CategoryId` stays non-null while the DB has `null`; a subsequent `SaveChanges` would overwrite the DB back. Verify no
  tracked products overlap with the bulk update, or invalidate affected entries after the call.
- [ ] **Missing `CategorySoftDeletedNotification`** — category soft-delete (both `DeleteCategoriesCommand` and
  `TenantCascadeDeleteHandler`) publishes no notification. Product soft-delete publishes
  `ProductSoftDeletedNotification` which Reviews consumes. Any future module needing to react to category deletion has
  no hook. Add a `CategorySoftDeletedNotification` and publish it from both delete paths.

### Low Priority

- [ ] **Aggregate boundary violation** — `Product` entity has `Category? Category` navigation property — a direct
  reference to another aggregate root. Replace with `CategoryId`-only reference; load via query when needed.
- [ ] **Missing value objects** — `Email` (string with no RFC validation), `Rating` (int with no range enforcement),
  `Price` (no currency/precision semantics), `TenantCode` (string with implicit format rules) should be strong value
  objects enforcing their invariants.
- [x] **Duplicate repository interfaces** — `IProductRepository` is defined in both `ProductCatalog.Domain/Interfaces/`
  and `ProductCatalog.Application/Features/Product/Repositories/`. Keep one definition in the Domain layer.
- [ ] **Integration test gap — `ProductDataLinks` cascade not verified** — `PostgresTenantSoftDeleteCascadeTests`
  verifies products and categories are soft-deleted but does not assert `ProductDataLinks` are also soft-deleted in the
  same cascade. Add assertion to guard against silent regression.
- [x] **`ProductDataLink` unique constraint** — `Product.SyncProductDataLinks` was previously guarded with
  `GroupBy().First()` to survive duplicate `ProductDataId` entries; simplified to `ToDictionary()` which throws on
  duplicates. Verify a unique constraint on `(ProductId, ProductDataId)` exists in the schema; add the migration if
  missing.

---

## Wolverine Outbox & Durable Messaging

- [ ] Enable `UseDurableOutboxOnAllSendingEndpoints()` and `UseDurableInboxOnAllListeners()` for reliable eventual
  consistency across modules.
- [x] Configure `PersistMessagesWithPostgresql()` for durable message persistence in PostgreSQL.
- [ ] Apply `DurabilityMode.Balanced` via shared Wolverine conventions (`ApplySharedConventions()`).
- [x] Migrate handler return types to `(ErrorOr<T>, OutgoingMessages)` tuples for transactional cascade messages instead
  of manual `bus.PublishAsync()`.
- [ ] Extract `CacheInvalidationCascades` helper (`.ForTag()`, `.ForTags()`, `.None`) to eliminate cache invalidation
  boilerplate.

## Wolverine Validation Middleware

- [x] Implement `ErrorOrValidationMiddleware` as Wolverine `Before` middleware — automatic FluentValidation for all
  commands without manual validation in handlers.
- [x] Add `FluentValidationActionFilter` for MVC controller endpoints (validates action parameters via DI-resolved
  validators, returns 400 with `ValidationProblemDetails`).

## Integration Events

- [x] Define typed integration event contracts in shared contracts (`SharedKernel.Contracts.Events`) (e.g.
  `ProductSoftDeletedNotification`, `TenantSoftDeletedNotification`).
- [x] Add integration event handlers per module for cross-module cascade operations (soft-delete propagation, cleanup).

## Request Context & Observability Enhancements

- [x] Enhance `RequestContextMiddleware` with tenant ID extraction from claims and Activity tag enrichment for
  distributed tracing.
- [x] Add `IHttpMetricsTagsFeature` enrichment (api_surface, authenticated) for custom telemetry dimensions.
- [x] Return `X-Trace-Id` response header alongside existing `X-Correlation-Id` and `X-Elapsed-Ms`.
- [x] Enhance Serilog request logging with intelligent log levels (499 client abort vs 5xx server error vs 4xx
  validation).
- [x] Enrich Serilog diagnostic context with `RequestHost` and `RequestScheme`.

## Logging Redaction

- [x] Implement data classification for logging (Personal, Sensitive categories).
- [x] Configure HMAC redaction for sensitive data and erasing redaction for personal data.
- [x] Add environment-based HMAC key resolution from configuration.

## Authentication & Authorization Enhancements

- [x] Add tenant claim validation in JWT bearer configuration — require valid tenant claim or service account prefix.
- [x] Add `KeycloakClaimsPrincipalMapper.MapClaims()` for Keycloak claim transformation.
- [x] Add `AuthorizationResponsesOperationTransformer` for OpenAPI — automatically document 401/403 on `[Authorize]`
  endpoints.
- [x] Add `BearerSecuritySchemeDocumentTransformer` — dynamic Keycloak OAuth2 authorization code flow in OpenAPI.

## Exception Handling Enhancements

- [ ] Enhance `ApiExceptionHandler` with structured error metadata preservation in
  `ProblemDetails.Extensions["metadata"]`.
- [ ] Add error code fallback logic (check `exception.ErrorCode` then `metadata["errorCode"]` then
  `ErrorCatalog.General.Unknown`).
- [x] Differentiate logging by status code (LogError for 5xx, LogWarning for handled exceptions).

## Output Caching Enhancements

- [x] Add `TenantAwareOutputCachePolicy` — cache key isolation per tenant to prevent cross-tenant data leaks.
- [x] Expand cache policies to cover all implemented cacheable API resources (Tenants, TenantInvitations, Users alongside
  existing Products, Categories, Reviews, ProductData).

## Controller Base Enhancements

- [ ] Add helper methods to `ApiControllerBase`: `InvokeToActionResultAsync<T>()`, `InvokeToBatchResultAsync()`,
  `InvokeToNoContentResultAsync()`, `InvokeToOkResultAsync()`, `InvokeToCreatedResultAsync()`.
- [ ] Add `ErrorOrHttpExtensions` for minimal API ErrorOr-to-ProblemDetails mapping.

## Configuration Validation

- [x] Implement `AddValidatedOptions<TOptions>()` extension — automatic DataAnnotations validation with early startup
  failure on invalid configuration.

## Idempotency

- [x] Implement `IdempotencyActionFilter` — at-most-once semantics via `Idempotency-Key` header with cached responses,
  configurable TTL, lock timeouts, and 409 Conflict on concurrent processing.

## Health Check Helpers

- [x] Extract health check helper extensions: `AddPostgreSqlHealthCheck()`, `AddDragonflyHealthCheck()` with
  standardized tags and naming.

## Infrastructure Generics

- [x] Make `UnitOfWork` generic over `DbContext` instead of hardcoded to `AppDbContext` — enables reuse across
  per-module contexts.
- [x] Make `RepositoryBase<T>` accept generic `DbContext` parameter instead of casting to `AppDbContext`.
- [x] Extract `TenantAuditableDbContext` as abstract reusable base class with `TenantAuditableDbContextDependencies`
  record for dependency encapsulation. (ModuleDbContext already serves this role)
- [ ] Make `IEntityNormalizationService` optional (nullable) in DbContext — not all modules need normalization.
- [ ] Improve `DesignTimeConnectionStringResolver` with dynamic path resolution (walk up directory tree) and
  environment-specific appsettings loading.

## Entity Navigation Properties

- [ ] Add explicit bidirectional navigation properties on aggregate roots (e.g. `Tenant.Users`, `AppUser.Tenant`) for
  better DDD modeling and EF Core relationship configuration.

## Validation Metrics

- [ ] Add `IValidationMetrics` interface for recording validation failures with telemetry (source, argument type,
  failure list) — separates observability from application logic.

---

## Observability

- [x] Add observability stack and instrumentation for metrics, tracing, and alerting.
- [x] Add OpenTelemetry for traces, metrics, and correlation across database, HTTP, and cache operations.

## User Workflows

- [x] Add user registration workflow.
- [x] Add user lifecycle workflows such as activation, deactivation, and role management.

## Tenant Management

- [x] Ensure tenant-scoping for Output Cache tags (prevent cross-tenant cache invalidation).
- [x] Add tenant creation workflow.
- [x] Add tenant removal workflow.

## Product Data

- [x] Add workflow for attaching `ProductData` records to products.
- [x] Support many-to-many relationship where a single product can have multiple `ProductData` entries.

## Notifications

- [x] Add email notification for user registration.
- [x] Add email notification for tenant invitation workflow.
- [x] Add email notification for password reset workflow.
- [x] Add email notification for user role changes.

## Real-Time Communication (SignalR)

Implement real-time notifications and chat using ASP.NET Core SignalR.

**Architecture:**

- NotificationHub: job status, data updates, user status
- ChatHub: 1:1, groups, channels
- Redis backplane for multi-instance
- Optional persistence (flexible, add later if needed)

**Implementation:**

- [ ] Setup SignalR infrastructure (Hubs, backplane, middleware)
- [ ] NotificationHub: job/product/user status updates
- [ ] ChatHub: 1:1 messaging
- [ ] ChatHub: group and channel messaging
- [ ] Authorization and connection management
- [ ] Client SDK (JavaScript/TypeScript)
- [ ] Message persistence layer (pluggable design)
- [ ] Error handling, reconnection, idempotency

## Contracts

- [ ] Extract request/response DTOs and shared contract models into a separate NuGet package.

## Search

- [x] Add full-text search for products and categories.
- [x] Add faceted filtering for search results.

## Background Jobs

- [x] Add cleanup jobs for expired or orphaned data.
- [x] Add reindex jobs for search data.
- [x] Add retry jobs for failed notifications.
- [x] Add periodic synchronization tasks for external integrations.
- [x] Cursor-based pagination for orphaned ProductData cleanup to bound memory usage at scale.
- [x] Distributed locking (`SELECT ... FOR UPDATE SKIP LOCKED` or claim column) for email retry to prevent duplicate
  sends in multi-instance deployments.
- [x] Migrate from `PeriodicTimer` to Quartz.NET (or TickerQ) for CRON scheduling, persistent job state, and distributed
  locking.

## Permissions

- [x] Add a finer-grained permissions model beyond roles.
- [x] Add policy-based access control per action and resource.

## File and Media Handling

- [ ] Add file upload support for `ProductData`.
- [ ] Add storage abstraction for local and S3-compatible backends.
- [ ] Add cleanup workflow for orphaned files.

## Soft delete and Data Retention

- [x] Hard delete for soft-deleted products after a configurable retention period.
- [x] Add workflow for permanently deleting soft-deleted products after retention period.
- [ ] Wolverine durable outbox or CAP for reliable messaging and eventual consistency in data deletion across related
  entities. (WolverineFx is now integrated as the in-process mediator; durable outbox mode can be enabled when needed.)

## Result Pattern

- [x] Introduce `Result<T>` pattern (e.g. via `OneOf` or custom type) for expected failures instead of exceptions as
  flow control.
- [x] Migrate validation, not-found, and conflict scenarios from exceptions to explicit return types.

## Testing Improvements

- [x] Migrate key integration tests from in-memory EF Core to Testcontainers PostgreSQL for realistic database behavior.
- [x] Add tests covering PostgreSQL-specific behavior: migrations, `xmin` concurrency tokens, full-text search queries.
- [ ] Add infrastructure smoke tests (startup validation, OpenAPI parity across modules).
- [ ] Extract shared test utilities into `Tests.Common` library (`AsyncPoll` for eventual consistency,
  `TestDatabaseLifecycle`, `TestDataHelper`).
- [ ] Implement abstract `ServiceFactoryBase<TProgram>` for consistent `WebApplicationFactory` configuration across
  module tests.

## Modularization (Phase 1)

- [x] Split `AppDbContext` into per-module contexts (ProductCatalogDbContext, ReviewsDbContext, IdentityDbContext,
  etc.).
- [x] Replace direct cross-module calls (soft-delete cascade rules) with Wolverine integration events.
- [ ] Add ArchUnitNET or NetArchTest architecture tests to enforce module boundaries.
- [x] See `TODO-Architecture.md` for full modular monolith plan.

## Prioritization

### High Priority

**Tenant Management** — Tenant creation and removal workflows are core functionality for a multi-tenant system. Without
them, tenants cannot be fully managed — currently only a bootstrap tenant exists via configuration. Includes tenant
creation, admin assignment, deactivation, and complete removal with cascading cleanup of all related data (users,
products, categories).

**Notifications** — Email infrastructure is fully in place (SMTP client, FailedEmail entity, retry jobs with distributed
locking). Only business logic is missing — email templates and handlers for registration, tenant invitation, password
reset, and role changes. Minimal effort with high UX impact.

**Wolverine Outbox & Handler Tuples** — Enable durable outbox with PostgreSQL persistence and migrate handlers to
`(ErrorOr<T>, OutgoingMessages)` return types. Provides transactional message delivery guarantees without external
message broker. Foundation for reliable cross-module communication.

**Wolverine Validation Middleware** — `ErrorOrValidationMiddleware` eliminates manual FluentValidation calls in every
handler. Automatic, consistent validation across all commands with proper ErrorOr integration. Low effort, high
consistency impact.

### Medium Priority

**Modularization (Phase 1)** — Split the monolith into isolated modules (ProductCatalog, Reviews, Identity,
Notifications, FileStorage, BackgroundJobs, Webhooks). Includes splitting `AppDbContext` into per-module contexts,
replacing direct cross-module calls with Wolverine integration events, and adding architecture tests to enforce
boundaries. Prepares the project for future extraction without changing business logic. See `TODO-Architecture.md` for
the full plan.

**Request Context & Observability** — Enhance middleware with tenant tracing, metrics enrichment, and intelligent
Serilog log levels. Improves debugging, monitoring, and distributed trace correlation with minimal code changes.

**Exception Handling & Logging Redaction** — Structured error metadata in ProblemDetails, differentiated log levels by
status code, and data classification for log redaction (HMAC for sensitive, erase for personal). Security and
observability improvement.

**Authentication Enhancements** — Tenant claim validation in JWT, Keycloak claims mapping, and OpenAPI security
transformers. Strengthens multi-tenant security and improves API documentation accuracy.

**Testing Improvements** — Migrate key integration tests from in-memory EF Core to Testcontainers PostgreSQL for
realistic database behavior. The in-memory provider does not capture PostgreSQL-specific behavior — `xmin` concurrency
tokens, full-text search, migrations, JSON operators. Testcontainers setup already exists in the project and needs to be
extended to critical test suites.

**Infrastructure Generics** — Make `UnitOfWork` and `RepositoryBase` generic over `DbContext`. Required for per-module
context split (Modularization Phase 1) and eliminates tight coupling to `AppDbContext`.

### Lower Priority

**Controller Base Helpers** — Reduce controller boilerplate with `InvokeToActionResultAsync<T>()` and similar methods.
Quality-of-life improvement.

**Configuration Validation** — `AddValidatedOptions<TOptions>()` catches invalid configuration at startup instead of
runtime. Prevents production configuration bugs.

**Output Caching** — Tenant-aware cache policy and expanded coverage. Prevents cross-tenant data leaks and improves
cache hit rates.

**Idempotency** — `IdempotencyActionFilter` for at-most-once semantics on mutation endpoints. Important for webhook
receivers and external API integrations.

**Result<T> Pattern** — Gradually migrate from exceptions (`ValidationException`, `NotFoundException`) to explicit
return types for expected failures. Removes exception throwing overhead in common scenarios and makes method signatures
more transparent. Best introduced incrementally, starting with new features.

**Contracts NuGet Package** — Extract request/response DTOs into a standalone package. Allows clients to reference only
contracts without depending on the Application layer. Essential for sharing types with frontend clients.

**Permissions** — Extend the 3-tier role model (PlatformAdmin, TenantAdmin, User) with finer-grained policy-based access
control. Per-action and per-resource permissions enable more granular access control without needing to create new roles
for every combination of privileges.
