# TODO

## Testing Backlog

### Integration

#### Medium Priority

- [ ] **Add cache invalidation/output-cache integration coverage**  
  Verify that product, category, product-data, and review mutations evict the expected output-cache entries or tags across module boundaries.

- [ ] **Add GraphQL feature integration coverage beyond basic query health**  
  Existing tests cover basic GraphQL availability and product query validation. Add coverage for product/category filters, not-found/error mapping, authorization behavior, and any supported mutations.

#### Low Priority

- [ ] **Add PostgreSQL integration coverage for bulk soft-delete operations**  
  Add real-database tests for category/product/review bulk soft-delete paths to verify query filters, tenant boundaries, audit metadata, and idempotent repeat execution.

- [ ] **Add integration coverage for cleanup jobs that cross module boundaries**  
  Verify orphaned product-data cleanup and soft-delete cleanup through the real module registrations, not only isolated service or handler tests.

### Smoke

#### Medium Priority

- [ ] **Add Identity invitation smoke test**  
  Existing identity smoke checks list endpoints. Add one minimal invitation create/resend/revoke path or a narrower create/revoke path if token acceptance requires external email flow.

#### Low Priority

- [ ] **Add GraphQL smoke test for a real domain query**  
  Keep the existing `__typename` smoke test, and add one authenticated domain query such as products with paging to catch schema resolver wiring regressions.

### Edge-Case Coverage

#### High Priority

- [ ] **Test batch operations with partial failure and duplicate IDs**  
  Cover create/update/delete requests where some items are valid, some are invalid, IDs are duplicated, IDs are empty, or the same entity is referenced more than once. Verify failure indexes, success counts, failure counts, and that duplicate IDs do not cause double updates, double deletes, or misleading responses.

- [ ] **Test tenant-boundary violations on reads, writes, and soft deletes**  
  Verify that products, categories, reviews, product data, roles, users, tenant invitations, BFF sessions, and files cannot be read, updated, deleted, or counted from another tenant. Include bulk operations and query filters because these are the most likely places for accidental cross-tenant leakage.

- [ ] **Test soft-delete idempotency and already-deleted records**  
  Cover deleting already soft-deleted products/categories/reviews/product data/files, repeated batch delete requests, querying deleted records by id/list, and cascading handlers receiving events for entities that are already deleted or missing.

- [ ] **Test transaction failure behavior in multi-step handlers**  
  For handlers such as category deletion and product deletion, simulate failure after the first repository call and verify the unit of work rolls back, no partial state is committed, and no cache invalidation/domain messages are emitted before the transaction succeeds.

#### Medium Priority

- [ ] **Test cache invalidation when no rows are affected**  
  Verify whether handlers should emit cache invalidation messages when updates/deletes affect zero records, when cascade handlers receive empty ID collections, or when a referenced entity is missing. Lock in the intended behavior so caches are not stale or over-invalidated.

- [ ] **Test category/product relationship edge cases**  
  Cover deleting a category with no products, with many products, with products already soft-deleted, and with products from another tenant. Verify `CategoryId` is cleared only where expected and category stats remain correct after deletion.

- [ ] **Test review aggregate edge cases**  
  Cover multiple reviews for the same product/user if allowed or explicitly rejected, reviews for deleted products, reviews for missing products, boundary rating values, empty/maximum comment lengths, and deletion of reviews already removed by product cascade.

- [ ] **Test ProductData link edge cases**  
  Cover products linked to missing product data, duplicate product-data links, deleting product data still linked to products, deleting unlinked product data, and querying product data after linked products are soft-deleted.

- [ ] **Test FileStorage lifecycle edge cases**  
  Cover commit with wrong hash, wrong size, expired staging token, reused staging token, missing staged blob, duplicate content hash, download after delete, delete during pending saga, and orphan sweep when files are still referenced.

- [ ] **Test identity/security edge cases**  
  Cover disabled users, deleted users with active BFF sessions, role updates that remove the caller's own permissions, immutable role mutation attempts, stale permission claims after role changes, expired invitations, revoked invitations, accepted invitations reused twice, and password reset for missing/unlinked users.

- [ ] **Test query/filter/sort boundary values**  
  Cover page number/page size minimums and maximums, unknown sort fields, invalid sort directions, empty filters, whitespace-only filters, case-insensitive matching, min/max price boundaries, date range boundaries, and filters combined with soft-deleted data.

#### Low Priority

- [ ] **Test concurrency and race-prone paths**  
  Add targeted tests for concurrent create/update/delete requests that touch the same product/category/review/file/session, especially optimistic concurrency, duplicate invitation creation, duplicate role names, and simultaneous upload commits.

- [ ] **Test serialization and error-shape edge cases**  
  Cover malformed JSON, missing required body, wrong content type, unsupported enum/string values, invalid GUID route values, empty request bodies, and consistent `ProblemDetails`/error-code responses across REST, Wolverine HTTP, and GraphQL.

- [ ] **Test cleanup retention boundary conditions**  
  Cover cleanup jobs at exactly the retention cutoff, just before cutoff, just after cutoff, empty batches, batch size limits, repeat execution, and cleanup when dependent module data is missing.


## Infrastructure & Architecture

### High Priority

- [x] **Centralize shared infrastructure mapping in `ApplicationBuilderExtensions`**  
  Shared infrastructure such as `MapGraphQL()` is currently hidden inside `ProductCatalogModule.cs`. This violates module isolation and the Composition Root pattern. Move `MapGraphQL()` to `ApplicationBuilderExtensions.cs` and ensure modules only contribute to the schema via `AddXXXGraphQL()`.

- [x] **Eliminate boilerplate module endpoint mapping**  
  Since Wolverine and Controllers handle automatic endpoint discovery, removed all empty `MapXXXEndpoints` methods across modules to strictly adhere to YAGNI and reduce cognitive load.

- [ ] **Finalize Program.cs cleanup after module extraction**  
  Once all modules (Reviews, Chatting) are fully isolated, perform the final rewiring in `Program.cs` to remove legacy project references and redundant service registrations.


## Features

### Medium Priority

- [ ] **Complete file/media story around `ProductData`**  
  Orphan sweep workflow exists end-to-end. What remains: `ProductData` / `ImageProductData` / `VideoProductData` have no `StoredFileId` or any reference to the `FileStorage` module — the file link is entirely absent. Only `LocalBlobStore` is implemented; no S3/Azure/Minio backend exists.

### Low Priority

- [ ] **Add missing bidirectional navigations only where they add real value**  
  If richer aggregate modeling is introduced, add explicit navigations such as `Tenant.Users` / `AppUser.Tenant` only where they simplify invariants or EF configuration rather than just increasing graph size.

- [ ] **Introduce validation telemetry abstraction**  
  Add something like `IValidationMetrics` only if validation failure observability becomes a real operational need; current validation behavior exists, but telemetry is not separated yet.

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
