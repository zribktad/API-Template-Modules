# TODO

## Testing Backlog

### UNIT

#### High Priority

- [ ] **Cover ProductCatalog category command/query handlers beyond delete**  
  Missing focused unit coverage for `CreateCategoriesCommandHandler`, `UpdateCategoriesCommandHandler`, `GetCategoriesQueryHandler`, `GetCategoryByIdQueryHandler`, and `GetCategoryStatsQueryHandler`. Existing tests cover category delete behavior and batch-rule DI resolution, but not create/update/query success and failure paths.

- [ ] **Cover ProductData command/query handlers**  
  Missing focused unit coverage for `CreateImageProductDataCommandHandler`, `CreateVideoProductDataCommandHandler`, `DeleteProductDataCommandHandler`, `GetProductDataQueryHandler`, and `GetProductDataByIdQueryHandler`. Existing tests cover cascade delete/pipeline behavior, but not the command/query handlers themselves.

- [ ] **Cover Reviews create/delete/query handlers**  
  Missing focused unit coverage for `CreateProductReviewCommandHandler`, `DeleteProductReviewCommandHandler`, `GetProductReviewsQueryHandler`, `GetProductReviewByIdQueryHandler`, `GetProductReviewsByProductIdQueryHandler`, and `GetProductReviewsByProductIdsQueryHandler`. Existing tests cover rating value object validation, boundary validation, and product-soft-delete cascade behavior.

#### Medium Priority

- [ ] **Cover cache invalidation cascade contracts**  
  Add regression tests for `ProductCatalog.Common.Events.CacheInvalidationCascades` and `Reviews.Common.Events.CacheInvalidationCascades` so product/category/product-data/review changes keep invalidating the expected cache tags.

- [ ] **Cover FileStorage endpoint command gaps**  
  Existing unit tests cover begin/commit upload, saga lifecycle, local blob store, maybe-delete, and orphan sweep behavior. Add focused tests for `UploadFileCommandHandler`, `DownloadFileQueryHandler`, and `DeleteFileCommandHandler`.

- [ ] **Cover tenant invitation command handlers**  
  Repository and notification email behavior are tested, but command-level behavior still needs focused unit tests for `CreateTenantInvitationCommandHandler`, `AcceptTenantInvitationCommandHandler`, `ResendTenantInvitationCommandHandler`, and `RevokeTenantInvitationCommandHandler`.

#### Low Priority

- [ ] **Cover simple projection/query mapping edge cases**  
  Add lightweight tests for category/product-data/review response projections only where they encode non-trivial behavior such as nullable fields, sorting aliases, or filter criteria.

### Integration

#### High Priority

- [ ] **Add category API CRUD integration coverage**  
  Current smoke coverage only verifies `GET /api/v1/categories` returns OK. Add authenticated integration tests for create, get by id, update, delete, get stats, not-found, validation failure, and permission failure paths.

- [ ] **Add product review API integration coverage**  
  Current coverage includes validation and smoke GET checks, but not an authenticated create/read/delete flow. Add tests for creating a review against an existing product, querying by product, deleting it, not-found behavior, and permission failure paths.

- [ ] **Add ProductData API integration coverage**  
  Current smoke coverage only verifies `GET /api/v1/product-data` returns OK. Add tests for creating image/video product data, reading by id/list, deleting product data, not-found behavior, validation failure, and permission failure paths.

#### Medium Priority

- [ ] **Add FileStorage HTTP integration coverage**  
  Unit coverage exists for the core upload/saga pieces. Add API-level tests for begin upload, commit upload, download, delete, invalid extension/content-type rejection, and missing file behavior.

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

#### High Priority

- [ ] **Add write-path smoke tests for ProductCatalog and Reviews**  
  Current smoke tests mostly assert GET endpoints return OK. Add one minimal authenticated create-read-delete smoke flow for products, categories, and product reviews to catch broken routing, auth, Wolverine dispatch, and persistence wiring.

#### Medium Priority

- [ ] **Add FileStorage smoke test**  
  Add one minimal begin-upload/commit/download smoke path using the configured local blob store to catch broken storage registration and endpoint wiring.

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


## Features


### Medium Priority

- [ ] **Complete file/media story around `ProductData`**  
  Orphan sweep workflow exists end-to-end. What remains: `ProductData` / `ImageProductData` / `VideoProductData` have no `StoredFileId` or any reference to the `FileStorage` module — the file link is entirely absent. Only `LocalBlobStore` is implemented; no S3/Azure/Minio backend exists.

- [ ] **Strengthen exception error-code fallback behavior**  
  `ApiExceptionHandler` already checks `IHasErrorCode` and falls back to `ErrorCatalog.General.Unknown`. What remains: the middle step — if the exception does not implement `IHasErrorCode`, inspect `metadata["errorCode"]` before falling back to `Unknown`.

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
