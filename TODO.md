# TODO


## Medium Priority

- [ ] **Complete file/media story around `ProductData`**  
  Orphan sweep workflow exists end-to-end. What remains: `ProductData` / `ImageProductData` / `VideoProductData` have no `StoredFileId` or any reference to the `FileStorage` module — the file link is entirely absent. Only `LocalBlobStore` is implemented; no S3/Azure/Minio backend exists.

- [ ] **Strengthen exception error-code fallback behavior**  
  `ApiExceptionHandler` already checks `IHasErrorCode` and falls back to `ErrorCatalog.General.Unknown`. What remains: the middle step — if the exception does not implement `IHasErrorCode`, inspect `metadata["errorCode"]` before falling back to `Unknown`.

## Low Priority

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
