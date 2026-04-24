# TODO

## High Priority

- [x] **Enrich domain model and remove handler-centric business logic**  
  A lot of behavior is still concentrated in handlers (`CreateProductsCommand`, `CreateUserCommand`, file workflows). Move invariants and state transitions into entities/factories/domain services so handlers orchestrate instead of constructing business rules inline.

- [ ] **Stabilize soft-delete cascade semantics**  
  There is still overlap between multiple cascade mechanisms, and category delete still has no dedicated integration event. Decide on one authoritative cascade path and resolve the `ClearCategoryAsync()` tracker hazard around `ExecuteUpdateAsync`.

- [x] **Add architecture tests for module boundaries**  
  Added `NetArchTest`-based guardrails in `tests/APITemplate.Tests/Unit/Architecture` to block new direct module-to-module coupling.
  Temporary exception remains explicit: `ProductCatalog -> Reviews` is still allowed until that dependency is refactored away.

## Medium Priority

- [ ] **Finish durable messaging conventions cleanup**  
  Durable PostgreSQL persistence, inbox, and outbox are already enabled. What remains is standardizing conventions such as shared Wolverine setup (`DurabilityMode.Balanced` if still desired) and reducing repetitive cache invalidation message construction with a helper such as `CacheInvalidationCascades`.

- [ ] **Complete file/media story around `ProductData`**  
  Generic file upload/download exists in `FileStorage`, but `ProductData` is not yet connected to uploaded files, storage backends are still effectively local-only, and orphaned stored files still need a cleanup workflow.

- [ ] **Strengthen exception error-code fallback behavior**  
  `ApiExceptionHandler` already preserves `metadata`, but fallback resolution is still shallow. Extend it to prefer `exception.ErrorCode`, then `metadata["errorCode"]`, then `ErrorCatalog.General.Unknown`.

- [ ] **Add infrastructure-level smoke coverage consolidation**  
  Startup and OpenAPI tests already exist, but test infrastructure is still fragmented. Consolidate shared integration helpers into a common base/utilities package if test maintenance keeps growing.

## Low Priority

- [ ] **Add missing bidirectional navigations only where they add real value**  
  If richer aggregate modeling is introduced, add explicit navigations such as `Tenant.Users` / `AppUser.Tenant` only where they simplify invariants or EF configuration rather than just increasing graph size.

- [ ] **Introduce validation telemetry abstraction**  
  Add something like `IValidationMetrics` only if validation failure observability becomes a real operational need; current validation behavior exists, but telemetry is not separated yet.

- [ ] **Decide whether controller/minimal-API helper abstraction is still worth it**  
  `ErrorOrExtensions` already cover most response mapping. Add extra `ApiControllerBase` / minimal API helpers only if future endpoints start duplicating mapping logic again.

- [ ] **Evaluate hybrid BFF session validation for high-traffic deployments**  
  Keep as an optimization backlog item only if per-request refresh/session validation becomes measurably expensive.
  - [ ] Add metrics: `CookieSessionRefresher.ValidatePrincipal` duration histogram, Redis GET p50/p99, Postgres fallback rate, % requests hitting `IsRefreshRequired = true`. *(Highest leverage — without numbers every other item is guesswork.)*
  - [ ] Evaluate skip-window validation: short in-process cache (e.g. 5–15 s) on top of `BffRequestScopedSessionCache` to skip store lookup for recently validated sessions. *(Biggest per-request latency win; directly removes Redis GET from the hot path.)*
  - [ ] Evaluate pub/sub-driven revocation invalidation (Redis keyspace notifications or Wolverine broadcast) so local caches drop revoked sessions without per-request store reads. *(Enables safe skip-window / longer TTLs by keeping revocation near-instant.)*
  - [ ] Review `BffOptions.CacheTtlMinutes` / `RefreshThresholdMinutes` against measured refresh-storm behavior; confirm `IBffRefreshCoordinator` absorbs thundering-herd at target load. *(Cheap tuning before any code change.)*
  - [ ] Consider decoupling `ValidatePrincipal` from `RefreshIfRequiredAsync`: only validate on request, schedule refresh in a background hosted service when expiry nears. *(Removes Keycloak call from request path; bigger refactor.)*
  - [ ] Define revocation-latency SLO (how stale may an active session be after `RevokeAsync`) so hybrid trade-offs can be judged against a concrete number. *(Needed before accepting any skip-window design.)*
  - [ ] Benchmark `BffSessionValidator` + `PostgresDistributedCacheBffSessionStore` to confirm whether CPU or I/O dominates before choosing a hybrid strategy. *(Only useful once metrics flag a hotspot.)*
  - [ ] Add a load-test profile (smoke/load fixtures) that reproduces target RPS per node and measures BFF session path under sustained traffic. *(Validation step after a hybrid variant is implemented.)*

- [ ] **Extract shared contracts into a separate package only when there is a real consumer**  
  Keep DTO/contracts extraction as a packaging step for external clients, not as a refactor for its own sake.

- [ ] **SignalR remains optional future work**  
  Real-time infrastructure via SignalR (`NotificationHub`, `ChatHub`, backplane, persistence) is not implemented. Keep it only if the project is actually moving beyond the current HTTP/SSE shape.
