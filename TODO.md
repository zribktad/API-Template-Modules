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

- [ ] **Extract shared contracts into a separate package only when there is a real consumer**  
  Keep DTO/contracts extraction as a packaging step for external clients, not as a refactor for its own sake.

- [ ] **SignalR remains optional future work**  
  Real-time infrastructure via SignalR (`NotificationHub`, `ChatHub`, backplane, persistence) is not implemented. Keep it only if the project is actually moving beyond the current HTTP/SSE shape.
