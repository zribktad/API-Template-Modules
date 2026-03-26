# TODO

## Observability

- [x] Add observability stack and instrumentation for metrics, tracing, and alerting.
- [x] Add OpenTelemetry for traces, metrics, and correlation across database, HTTP, and cache operations.

## User Workflows

- [x] Add user registration workflow.
- [x] Add user lifecycle workflows such as activation, deactivation, and role management.

## Tenant Management

- [ ] Add tenant creation workflow.
- [ ] Add tenant removal workflow.

## Product Data

- [x] Add workflow for attaching `ProductData` records to products.
- [x] Support many-to-many relationship where a single product can have multiple `ProductData` entries.

## Notifications

- [ ] Add email notification for user registration.
- [ ] Add email notification for tenant invitation workflow.
- [ ] Add email notification for password reset workflow.
- [ ] Add email notification for user role changes.

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
- [x] Distributed locking (`SELECT ... FOR UPDATE SKIP LOCKED` or claim column) for email retry to prevent duplicate sends in multi-instance deployments.
- [x] Migrate from `PeriodicTimer` to Quartz.NET (or TickerQ) for CRON scheduling, persistent job state, and distributed locking.

## Permissions

- [ ] Add a finer-grained permissions model beyond roles.
- [ ] Add policy-based access control per action and resource.

## File and Media Handling

- [ ] Add file upload support for `ProductData`.
- [ ] Add storage abstraction for local and S3-compatible backends.
- [ ] Add cleanup workflow for orphaned files.


## Soft delete and Data Retention
- [x] Hard delete for soft-deleted products after a configurable retention period.
- [x] Add workflow for permanently deleting soft-deleted products after retention period.
- [ ] Wolverine durable outbox or CAP for reliable messaging and eventual consistency in data deletion across related entities. (WolverineFx is now integrated as the in-process mediator; durable outbox mode can be enabled when needed.)

## Result Pattern

- [ ] Introduce `Result<T>` pattern (e.g. via `OneOf` or custom type) for expected failures instead of exceptions as flow control.
- [ ] Migrate validation, not-found, and conflict scenarios from exceptions to explicit return types.

## Testing Improvements

- [ ] Migrate key integration tests from in-memory EF Core to Testcontainers PostgreSQL for realistic database behavior.
- [ ] Add tests covering PostgreSQL-specific behavior: migrations, `xmin` concurrency tokens, full-text search queries.

## Modularization (Phase 1)

- [ ] Split `AppDbContext` into per-module contexts (ProductCatalogDbContext, ReviewsDbContext, IdentityDbContext, etc.).
- [ ] Replace direct cross-module calls (soft-delete cascade rules) with Wolverine integration events.
- [ ] Add ArchUnitNET or NetArchTest architecture tests to enforce module boundaries.
- [ ] See `TODO-Architecture.md` for full modular monolith plan.

## Wolverine Outbox

- [ ] Enable `UseDurableOutboxOnAllSendingEndpoints()` for reliable eventual consistency across modules.

## Prioritization

### High Priority

**Tenant Management** — Tenant creation and removal workflows are core functionality for a multi-tenant system. Without them, tenants cannot be fully managed — currently only a bootstrap tenant exists via configuration. Includes tenant creation, admin assignment, deactivation, and complete removal with cascading cleanup of all related data (users, products, categories).

**Notifications** — Email infrastructure is fully in place (SMTP client, FailedEmail entity, retry jobs with distributed locking). Only business logic is missing — email templates and handlers for registration, tenant invitation, password reset, and role changes. Minimal effort with high UX impact.

### Medium Priority

**Modularization (Phase 1)** — Split the monolith into isolated modules (ProductCatalog, Reviews, Identity, Notifications, FileStorage, BackgroundJobs, Webhooks). Includes splitting `AppDbContext` into per-module contexts, replacing direct cross-module calls with Wolverine integration events, and adding architecture tests to enforce boundaries. Prepares the project for future microservices extraction without changing business logic. See `TODO-Architecture.md` for the full plan.

**Testing Improvements** — Migrate key integration tests from in-memory EF Core to Testcontainers PostgreSQL for realistic database behavior. The in-memory provider does not capture PostgreSQL-specific behavior — `xmin` concurrency tokens, full-text search, migrations, JSON operators. Testcontainers setup already exists in the project and needs to be extended to critical test suites.

### Lower Priority

**Result<T> Pattern** — Gradually migrate from exceptions (`ValidationException`, `NotFoundException`) to explicit return types for expected failures. Removes exception throwing overhead in common scenarios and makes method signatures more transparent. Best introduced incrementally, starting with new features.

**Contracts NuGet Package** — Extract request/response DTOs into a standalone package. Allows clients to reference only contracts without depending on the Application layer. Essential for future microservices extraction and sharing types with frontend clients.

**Permissions** — Extend the 3-tier role model (PlatformAdmin, TenantAdmin, User) with finer-grained policy-based access control. Per-action and per-resource permissions enable more granular access control without needing to create new roles for every combination of privileges.
