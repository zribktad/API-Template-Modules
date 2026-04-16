# 🔍 Code Review — API-Template-Monolith

**Generated:** 2026-04-16 · **Branch:** `main` · **Method:** 6 parallel specialized agents (2 repo splits × 3 disciplines: Bugs, Security/Architecture, Performance/Style)

## 🔍 Executive Summary

Solid modular-monolith template built on .NET 9 / Wolverine / Marten / EF Core with consistent vertical-slice handlers, ErrorOr pipelines, Ardalis.Specification repositories, and BFF-flavoured auth. Biggest risks concentrate in three areas: (1) **webhook ingress/egress** — SSRF TOCTOU, missing replay protection, global filter registration, plaintext HTTP allowed; (2) **tenant isolation on bulk operations** — `IgnoreQueryFilters()` + caller-supplied tenant/product ids in both ProductCatalog and Reviews bulk-delete paths; (3) **secrets hygiene** — dev HMAC/Keycloak/webhook secrets committed, Mongo/Email options unvalidated. Code-quality findings are mostly N+1 and eager-materialisation patterns plus a few SRP/DIP leaks in Identity session services.

- **Total findings:** 101 (🔴 6 · 🟠 45 · 🟡 31 · 🔵 19)
- **Splits:** A = `APITemplate` + `SharedKernel` + `Contracts` + `Modules/{BackgroundJobs,Chatting,FileStorage,Identity}`; B = `Modules/{Notifications,ProductCatalog,Reviews,Webhooks}`
- **Tests:** excluded per user instruction.

---
---

## 🔴 BUGS (Correctness)

---

### #1 · `Modules/BackgroundJobs/Features/SubmitJob/SubmitJobCommand.cs:35` 🔴 [BUG] `catch (Exception)` swallows `OperationCanceledException`
> EnqueueAsync on a bounded channel throws OCE when the caller's token is cancelled. The unfiltered catch marks the job failed, writes a second DB transaction on a cancelled request, and returns `Error.Failure`, defeating `ApiExceptionHandler`'s 499 handling.

**Fix:**
```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
```

---

### #2 · `Modules/Identity/Auth/Security/Keycloak/KeycloakClaimMapper.cs:36` 🔴 [BUG] Unchecked `JsonDocument.Parse` on `realm_access` claim
> Parse is called on an IdP-supplied claim without try/catch. Non-object realm_access or non-array `roles` throws `JsonException`/`InvalidOperationException` out of `OnTokenValidated`, breaking auth for every request carrying such a token instead of skipping role mapping.

**Fix:** wrap Parse in `try/catch (JsonException)` and verify `roles.ValueKind == JsonValueKind.Array` before `EnumerateArray`.

---

### #3 · `Modules/Chatting/Features/SseController.cs:39` 🔴 [BUG] SSE `writer.WriteAsync` ignores cancellation token
> Only `FlushAsync` observes `ct`; the preceding string-overload `WriteAsync` does not accept a CT. A client disconnect during a write cannot be observed until the next iteration, blocking on TCP backpressure.

**Fix:**
```csharp
await writer.WriteAsync($"{SseDataPrefix}{json}\n\n".AsMemory(), ct);
```

---

### #4 · `Modules/Identity/ValueObjects/Email.cs:54` 🔴 [BUG] `default(Email)` produces null `Value` → NRE in operator/Normalize
> `readonly record struct Email` has only a private constructor; `default(Email)` leaves `Value` null. Implicit `operator string` returns non-nullable string; `Normalize()` calls `Value.ToUpperInvariant()` — both NRE on default/uninitialised values.

**Fix:**
```csharp
public string Value { get; } = string.Empty;
```

---

### #5 · `Modules/Webhooks/Services/OutgoingWebhookBackgroundService.cs:91` 🔴 [BUG] SSRF check is TOCTOU — HttpClient re-resolves DNS
> Also tracked as SECURITY below. Validator resolves DNS once, checks IPs, then hands the URL to `HttpClient.SendAsync` which performs its own DNS resolution. DNS rebinding returns public IP to validator and private IP (169.254.169.254, 127.0.0.1) to the HTTP layer.

**Fix:** pin the validated IP via `SocketsHttpHandler.ConnectCallback` enforcing the policy on the connect socket.

---

### #6 · `Modules/Notifications/Services/EmailRetryService.cs` 🔴 [BUG] Cancellation between SMTP-send success and DB commit causes duplicate send
> `RetryFailedEmailsAsync` uses the same `ct` for `_sender.SendAsync` and `_unitOfWork.CommitAsync`. Cancellation after successful SMTP but before commit loses the staged delete and retries on the next run — **duplicate email delivery**.

**Fix:** after the "point of no return" (successful SMTP), commit with `CancellationToken.None` and surface the OCE only to the outer loop.

---
---

## 🟠 SECURITY & ARCHITECTURE

### Secrets / Configuration

#### #7 · `src/APITemplate/Api/appsettings.Development.json:36` 🟠 Hardcoded HMAC redaction key committed
> `mV7XhO9YXNw1fGKxvRrQz6CkKUL5jvN3i8A0Jv3cL2Q=` in git. Shared across developer machines; weakens HMAC-based PII pseudonymisation; risks bleed into non-dev environments.
> **Fix:** rotate, move to user-secrets / `APITEMPLATE_REDACTION_HMAC_KEY` env.

#### #8 · `src/APITemplate/Api/appsettings.Development.json:19` 🟠 Dev Keycloak secret + webhook secret committed
> `Keycloak.credentials.secret = "dev-client-secret"`, `Webhook.Secret = "dev-webhook-secret-at-least-16-chars"`.
> **Fix:** user-secrets; keep only structural defaults in file.

#### #9 · `Modules/ProductCatalog/ProductCatalogModule.cs:54` 🟠 `MongoDbSettings` bound without validation; empty defaults
> `services.Configure<MongoDbSettings>` accepts empty ConnectionString/DatabaseName; Mongo throws only at first use.
> **Fix:** `AddValidatedOptions<>()` + `[Required][MinLength(1)]` + `.ValidateOnStart()`.

#### #10 · `Modules/Notifications/Contracts/EmailOptions.cs:38` 🟠 SMTP password bound as plain string without redaction
> Any options-dump middleware serialises it.
> **Fix:** require secret store; mark property for redaction in any diagnostics dumper.

#### #11 · `Modules/Webhooks/Contracts/WebhookOptions.cs:14` 🟠 Webhook secret `MinLength(16)` below HMAC-SHA256 guidance
> **Fix:** raise to 32 + document cryptographically-random requirement.

### SSRF / Webhook ingress & egress

#### #12 · `Modules/Webhooks/Services/OutgoingWebhookBackgroundService.cs:15` 🟠 Plain `http` allowed for outbound webhooks
> `AllowedSchemes = {"https","http"}`. Signed payloads still delivered cleartext.
> **Fix:** https-only by default; `AllowInsecureHttp` opt-in.

#### #13 · `Modules/Webhooks/Services/OutgoingWebhookBackgroundService.cs:105` 🟠 IPv6 private-range check misses IPv4-mapped IPv6
> `::ffff:10.0.0.5` bypasses the IPv4 private table.
> **Fix:** `IPAddress.MapToIPv4()` before range check; reject IsIPv6SiteLocal/LinkLocal/UniqueLocal.

#### #14 · `Modules/Webhooks/Features/WebhooksController.cs:21` 🟠 `[AllowAnonymous]` webhook receiver with no rate limit
> HMAC flood forces body reads + signature comparisons.
> **Fix:** `[EnableRateLimiting]` per-IP; short-circuit on missing headers before buffering.

#### #15 · `Modules/Webhooks/Security/HmacWebhookPayloadValidator.cs:20` 🟠 No replay de-duplication within 300s window
> Same `(timestamp,payload,signature)` triple replays fire the handler every time.
> **Fix:** persist seen EventIds with TTL ≥ tolerance window; reject duplicates.

#### #16 · `Modules/Webhooks/Security/WebhookSignatureResourceFilter.cs:56` 🟠 Filter reads full body into string without explicit cap
> Combined with global filter registration (#17), unauthenticated clients can force large-body reads.
> **Fix:** read up to `WebhookOptions.MaxBodyBytes` into pooled buffer; return 413.

#### #17 · `Modules/Webhooks/WebhooksModule.cs:22` 🟠 Signature filter registered **globally** on every controller
> `options.Filters.AddService<WebhookSignatureResourceFilter>()` attaches the filter to all MVC actions. No-ops unless attribute is present, but every request pays the cost/risk.
> **Fix:** `[ServiceFilter(typeof(WebhookSignatureResourceFilter))]` on WebhooksController only.

### Tenant isolation / Authorization

#### #18 · `Modules/ProductCatalog/Repositories/ProductRepository.cs:149` 🟠 `BulkSoftDeleteByTenantAsync` + `GetNonDeletedIdsByTenantAsync` use `IgnoreQueryFilters()` with caller-supplied `tenantId`
> Any module publishing `TenantSoftDeletedNotification` can wipe any tenant's catalog.
> **Fix:** internal visibility; require a signed/verified Identity-owned notification before executing.

#### #19 · `Modules/Reviews/Repositories/ProductReviewRepository.cs:30` 🟠 `BulkSoftDeleteByProductIdsAsync` bypasses tenant filter
> `IgnoreQueryFilters()` + `productIds.Contains(r.ProductId)`. Malicious/buggy caller can soft-delete foreign tenants' reviews.
> **Fix:** require tenantId; `.Where(r => productIds.Contains(r.ProductId) && r.TenantId == tenantId)`.

#### #20 · `Modules/ProductCatalog/Features/Product/PatchProduct/PatchProductCommand.cs:24` 🟠 Patch does not re-validate `CategoryId` tenant ownership
> JSON Patch can set `CategoryId` to a foreign-tenant Guid; handler trusts EF filter only.
> **Fix:** load `Category` via repo and verify TenantId match before saving.

#### #21 · `Modules/ProductCatalog/Features/Product/PatchProduct/PatchProductCommand.cs:40` 🟠 Raw patch exception message returned to client
> `DomainErrors.Patch.InvalidPatchDocument(ex.Message)` echoes library internals.
> **Fix:** log `ex`; return fixed `"Invalid JSON patch document"`.

### Anonymous / high-risk endpoints

#### #22 · `Modules/Identity/Directory/Features/V1/UsersController.cs:148` 🟠 `POST /api/v1/users/password-reset` anonymous, no rate limit
> Enables email-bombing and user enumeration via timing.
> **Fix:** per-IP + per-email rate-limit; identical response timing/status.

#### #23 · `Modules/Identity/Directory/Features/V1/TenantInvitationsController.cs:47` 🟠 Anonymous invitation-accept, no rate limit
> Brute-force online guessing of invitation tokens.
> **Fix:** rate-limit keyed by IP; uniform response for invalid/revoked/used.

#### #24 · `Modules/Chatting/Features/SseController.cs:25` 🟠 SSE endpoint has no concurrency cap
> Unbounded long-lived connections; permit-limit applies to request count, not active streams.
> **Fix:** concurrency limiter + max stream duration + per-user active-stream cap.

### BFF / CSRF / Session

#### #25 · `Modules/Identity/IdentityModule.cs:79` 🟠 `AddCors` registered but `UseCors()` never invoked
> Dead policy; future maintainer may enable `UseCors()` assuming defensive defaults.
> **Fix:** delete registration or wire `UseCors()` in `UseApiPipeline` between Authentication and Authorization.

#### #26 · `Modules/Identity/IdentityModule.Auth.cs:148` 🟠 BFF session cookie `SameSite=Lax` instead of `Strict`
> Combined with GET-allowed `/logout`, weakens CSRF.
> **Fix:** `SameSite = Strict` unless OIDC continuation demands Lax (document reason if so).

#### #27 · `Modules/Identity/Auth/Features/V1/BffController.cs:78` 🟠 Logout accepts both `GET` and `POST`
> Cross-site `<img src=.../logout>` can force sign-out.
> **Fix:** POST-only with CSRF header; if GET affordance needed, render confirmation page auto-submitting POST.

#### #28 · `Modules/Identity/Auth/Features/V1/BffController.cs:42` 🟠 Open-redirect mitigated only via `Url.IsLocalUrl`
> Protocol-relative / fragment edge cases known to bypass in framework versions.
> **Fix:** parse as `UriKind.Relative` and reject leading `//` or `\\`, or allow-list from config.

#### #29 · `APITemplate/Api/Middleware/CsrfValidationMiddleware.cs:27` 🟠 Bearer-auth bypass of CSRF when cookie also present
> `HasSuccessfulBearerAuthenticationAsync` short-circuits CSRF when Authorization header validates. A stolen cookie + any valid bearer skips CSRF.
> **Fix:** reject simultaneous cookie+bearer or always enforce CSRF when a BFF cookie is present.

#### #30 · `Modules/Identity/Auth/Security/Sessions/BffPostgresSessionStoreBase.cs:61` 🟠 Session store uses `IServiceScopeFactory` + `GetRequiredService<IdentityDbContext>()` (DIP violation)
> Infrastructure concerns leak into `Auth/Security/Sessions`; hidden lifetime; potential conflict with Wolverine EF transactional middleware.
> **Fix:** move to `Identity.Persistence`; inject `IDbContextFactory<IdentityDbContext>`.

#### #31 · `Modules/Identity/Auth/Handlers/CleanupExpiredBffSessionsHandler.cs:41` 🟠 Handler queries `IdentityDbContext` directly, bypassing repository pattern
> Same for `CleanupExpiredInvitationsHandler`. Duplicates business rules in ad-hoc LINQ.
> **Fix:** expose `PurgeExpiredAsync(cutoff, batchSize, ct)` on the respective repos.

### Identity / Keycloak

#### #32 · `Modules/Identity/Auth/Features/KeycloakEventWebhookController.cs:36` 🟠 Webhook silently returns 404 when ApiKey unconfigured
> Silent failure to receive Keycloak password-change events → stale BFF sessions remain valid after external password changes.
> **Fix:** `IValidateOptions` + `.ValidateOnStart()`; fail fast when enabled but unconfigured.

#### #33 · `Modules/Identity/Auth/Features/KeycloakEventWebhookController.cs:19` 🟠 `/internal/*` routes share Kestrel with public; no second line of defence
> Leaked ApiKey → force-logout any user by Keycloak ID.
> **Fix:** IP allow-list or mTLS in addition to shared secret; document proxy rules.

#### #34 · `Modules/Identity/Auth/Security/Keycloak/KeycloakAdminService.cs:225` 🟠 Password check via deprecated ROPC grant
> User's plaintext password transmitted to a secondary client; discouraged by Keycloak/OAuth 2.1.
> **Fix:** Keycloak step-up auth (`kc_action=UPDATE_PASSWORD`) or `max_age=0` fresh-ID-token flow.

#### #35 · `Modules/Identity/Auth/Security/Keycloak/KeycloakAdminTokenProvider.cs:108` 🟠 Full Keycloak error body logged on token failure
> May leak `error_description` containing secret/trace fragments.
> **Fix:** log `StatusCode` + extracted `error` code only.

#### #36 · `Modules/Identity/Auth/Security/SecureTokenGenerator.cs:22` 🟠 Unsalted SHA-256 for persisted invitation tokens
> No keyed construction; DB leak enables rainbow-table attacks and no rotation path.
> **Fix:** `HMACSHA256` with server key (or DataProtection-derived key); store key-id alongside hash.

### File storage / Content handling

#### #37 · `Modules/FileStorage/Features/FilesController.cs:22` 🟠 `RequestSizeLimit` hardcoded (10 MB) diverges from `FileStorageOptions.MaxFileSizeBytes`
> **Fix:** configure `KestrelServerOptions.Limits.MaxRequestBodySize` + `FormOptions.MultipartBodyLengthLimit` from bound options at startup.

#### #38 · `Modules/FileStorage/Features/FilesController.cs:28` 🟠 Upload trusts client-supplied `FileName` / `ContentType`
> Only extension whitelist; real magic-byte detection missing.
> **Fix:** detect actual type from bytes (e.g., MimeDetective); enforce server-side allow-list.

#### #39 · `Modules/FileStorage/Features/FilesController.cs:71` 🟠 Download streams file with client ContentType, no `Content-Disposition: attachment`
> MIME-sniffing browsers render HTML/JS from same origin → stored XSS.
> **Fix:** `Content-Disposition: attachment`, `X-Content-Type-Options: nosniff`, or serve from cookie-less subdomain.

### Database / SQL

#### #40 · `Modules/BackgroundJobs/Services/ReindexService.cs:64` 🟠 `ExecuteSqlRawAsync` with string-interpolated identifier
> `REINDEX INDEX CONCURRENTLY "{index}"` relies on regex `^[a-zA-Z_][a-zA-Z0-9_]*$`. Fragile — pattern-copy bugs re-introduce SQLi.
> **Fix:** `NpgsqlCommandBuilder.QuoteIdentifier(index)`.

### Clean Architecture / Module boundaries

#### #41 · `Modules/ProductCatalog/GlobalUsings.cs:15` 🟠 ProductCatalog imports `Reviews.Domain` + `Reviews.Features`
> Cross-module reference bypasses a `Contracts` namespace. Couples ProductCatalog to Reviews internals.
> **Fix:** introduce `Reviews.Contracts`; depend only on that.

#### #42 · `Modules/ProductCatalog/GraphQL/Mutations/ProductReviewMutations.cs:12` 🟠 Reviews aggregate's GraphQL mutations live inside ProductCatalog
> `CreateProductReview`/`DeleteProductReview` + `ProductReviewType` + `ProductReviewsByProductDataLoader` manipulate the Reviews aggregate from the wrong module.
> **Fix:** move into Reviews as Hot Chocolate type extensions; ProductCatalog only extends `ProductType.reviews` via cross-module contract.

#### #43 · `Modules/ProductCatalog/GraphQL/Mutations/ProductMutations.cs:41` 🟠 Mutation throws `new GraphQLException(string.Join("; ", batch.Failures[0].Errors))`
> Duplicates ErrorOr→GraphQL translation; leaks raw failure text.
> **Fix:** route through `ErrorOrGraphQLExtensions`.

### Misc

#### #44 · `Modules/Notifications/Services/MailKitEmailSender.cs:60` 🟠 SMTP uses legacy `ConnectAsync(host,port,useSsl:bool,...)` overload
> `UseSsl=false` on port 587 → no STARTTLS → credentials cleartext.
> **Fix:** `SecureSocketOptions.StartTlsWhenAvailable`; default to StartTls for 587.

#### #45 · `Modules/Notifications/Services/MailKitEmailSender.cs:14` 🟠 Singleton SmtpClient serialized behind `SemaphoreSlim(1,1)`
> Single-flight site-wide; SRP leak (send + connection lifecycle).
> **Fix:** Scoped/Transient + small SmtpClient pool; split send from lifecycle.

#### #46 · `Modules/ProductCatalog/ProductCatalogModule.cs:113` 🟠 GraphQL: depth 5 but no execution timeout / cost analysis / introspection gating
> DataLoader + deep queries = cheap DoS for authenticated users.
> **Fix:** `ModifyRequestOptions(o => o.ExecutionTimeout = 30s)`, `AddCostAnalyzer`, disable introspection in prod.

#### #47 · `Modules/Identity/Directory/Features/V1/TenantsController.cs:10` 🟠 Missing class-level `[Authorize(AuthenticationSchemes=...)]`
> Relies on global fallback; future action without `[RequirePermission]` silently permitted.
> **Fix:** add explicit class-level attribute mirroring `AccountController`.

#### #48 · `Modules/Chatting/ChattingModule.cs:11` 🟠 `AddChattingModule` accepts `IConfiguration` but never uses it
> Skeleton misleads maintainers; silent config drift.
> **Fix:** drop the parameter or bind `ChattingOptions`.

#### #49 · `Modules/Notifications/Services/EmailRetryService.cs:35` 🟠 Claim owner `"{MachineName}:{ProcessId}"` persisted to DB
> Internal topology disclosure in admin UI/logs.
> **Fix:** opaque stable id (Guid per instance or HMAC of hostname).

#### #50 · `APITemplate/Api/Extensions/Startup/ApplicationBuilderExtensions.cs:107` 🟠 OpenAPI/Scalar gated only by `IsDevelopment()`
> Misconfigured `ASPNETCORE_ENVIRONMENT=Development` in staging/prod exposes full schema anonymously.
> **Fix:** gate on `IsDevelopment()` **and** explicit `EnableOpenApi` feature flag; require `[Authorize]` in non-prod docs.

---
---

## 🟡 PERFORMANCE

### Split A (APITemplate / SharedKernel / BackgroundJobs / Chatting / FileStorage / Identity)

#### #51 · `Modules/BackgroundJobs/Services/JobProcessingBackgroundService.cs:53` 🟡 7 DB round-trips per job (progress commits)
> `uow.CommitAsync()` per 20/40/60/80/100% step.
> **Fix:** batch progress writes or flush once on completion.

#### #52 · `Modules/Identity/Auth/Handlers/CleanupExpiredBffSessionsHandler.cs:41` 🟡 Select-then-delete (two round-trips) instead of single `ExecuteDeleteAsync`
> **Fix:** mirror `CleanupExpiredInvitationsHandler.cs:29`: single `ExecuteDeleteAsync` on filtered+ordered+Take query.

#### #53 · `Modules/BackgroundJobs/TickerQ/TickerQRecurringJobRegistrar.cs:43` 🟡 `ToListAsync` then client `ToDictionary` with tracking
> **Fix:** `ToDictionaryAsync`; `.Where(x => ids.Contains(x.Id))` to trim set.

#### #54 · `Modules/Identity/Auth/Security/Sessions/BffPostgresSessionStoreBase.cs:206` 🟡 BulkRevoke: SELECT ids → ExecuteUpdate on same predicate
> **Fix:** single ExecuteUpdateAsync on the predicate; invalidate cache by subject-keyed index.

#### #55 · `Modules/Identity/Auth/Security/Sessions/BffPostgresSessionStoreBase.cs:220` 🟡 Sequential `IDistributedCache.RemoveAsync` in loop
> **Fix:** `Task.WhenAll` or batch Redis `KeyDelete`.

#### #56 · `Modules/Identity/Directory/Features/Tenant/Commands/DeleteTenantCommand.cs:33` 🟡 Load-and-delete for soft-delete instead of bulk `ExecuteUpdateAsync`
> **Fix:** single ExecuteUpdateAsync setting `IsDeleted/DeletedAtUtc/DeletedBy`.

#### #57 · `APITemplate/Api/Cache/OutputCacheInvalidationService.cs:27` 🟡 Sequential `EvictByTagAsync` in foreach
> **Fix:** `Task.WhenAll` across distinct tags; isolate failures.

#### #58 · `Modules/BackgroundJobs/Services/ReindexService.cs:51` 🟡 Sequential bloat-checks + REINDEX across indexes
> **Fix:** bulk bloat SP; bounded parallel REINDEX (DOP=2).

#### #59 · `SharedKernel/Infrastructure/Repositories/RepositoryBase.cs:62` 🟡 Extra `CountAsync` on empty-page edge case
> **Fix:** skip second Count; return PageOutOfRange when `pageNumber > 1` unconditionally.

#### #60 · `Modules/Identity/Directory/Features/User/AssignRoles/AssignUserRolesCommandHandler.cs:53` 🟡 `user.Roles.Clear()` + re-`Add` rewrites unchanged roles
> 2N pivot writes for no-op assignments.
> **Fix:** compute set difference; guard with `SetEquals`.

#### #61 · `Modules/BackgroundJobs/Services/JobProcessingBackgroundService.cs:41` 🟡 Second DI scope inside callback for `IMessageBus`
> **Fix:** resolve once in outer scope, pass through.

#### #62 · `Modules/BackgroundJobs/Features/SubmitJob/SubmitJobCommand.cs:23` 🟡 `ExecuteInTransactionAsync` wrapping single `AddAsync`
> Same in `UploadFileCommand.cs:39`.
> **Fix:** plain `AddAsync` + `CommitAsync` for single-write handlers.

#### #63 · `Modules/FileStorage/Services/LocalFileStorageService.cs:94` 🟡 `File.Exists` TOCTOU before `File.Delete`
> `File.Delete` already silent for missing files.
> **Fix:** drop the check; try/catch `FileNotFoundException` for reads.

#### #64 · `Modules/Identity/Auth/Security/Keycloak/KeycloakAdminTokenProvider.cs:85` 🟡 `using` on pooled `HttpClient`
> Short-circuits IHttpClientFactory handler pooling.
> **Fix:** drop the `using`.

#### #65 · `Modules/Identity/Auth/Security/Sessions/BffCsrfTokenService.cs:54` 🟡 `Encoding.UTF8.GetBytes(sessionId)` allocation on every unsafe request
> **Fix:** `GetByteCount` + stackalloc for short session ids.

#### #66 · `APITemplate/Api/Authorization/PermissionPolicyProvider.cs:22` 🟡 `Permission.All.Contains` O(n) per policy lookup
> **Fix:** back with `FrozenSet<string>` or check ConcurrentDictionary cache first.

#### #67 · `Modules/Identity/Directory/Features/User/CreateUser/CreateUserCommand.cs:21` 🟡 Sequential email + username uniqueness checks
> **Fix:** single query returning both exists flags; halves sign-up latency.

### Split B (Notifications / ProductCatalog / Reviews / Webhooks)

#### #68 · `Modules/ProductCatalog/Features/Product/GetProducts/GetProductsQuery.cs:18` 🟡 Three sequential awaits: paged + category facets + price facets
> **Fix:** combine into a single GroupBy server query, or parallel `Task.WhenAll` with `IDbContextFactory`.

#### #69 · `Modules/ProductCatalog/Repositories/ProductRepository.cs:62` 🟡 Category-facet `LEFT JOIN` + ternary default inside `GroupBy` key
> **Fix:** group by `CategoryId` alone; resolve names via small lookup.

#### #70 · `Modules/ProductCatalog/Repositories/ProductDataLinkRepository.cs:92` 🟡 `SoftDeleteActiveLinksForProductData` materialises then row-by-row delete
> **Fix:** single `ExecuteDeleteAsync`.

#### #71 · `Modules/ProductCatalog/Features/Product/UpdateProducts/UpdateProductsCommand.cs:107` 🟡 Redundant per-item `await UpdateAsync` inside loop on tracked entities
> **Fix:** remove; SaveChanges flushes tracked changes.

#### #72 · `Modules/ProductCatalog/Features/Product/Shared/ProductValidationHelper.cs:128` 🟡 `missingIds.Contains` check with `Distinct().ToList()` allocation per item
> **Fix:** inline HashSet filter; drop Distinct when pdIds already deduped.

#### #73 · `Modules/ProductCatalog/Handlers/CleanupOrphanedProductDataHandler.cs:147` 🟡 Redundant server-side `Distinct()` on projected PK column
> **Fix:** drop `Distinct()`; let `ToHashSet()` dedupe client-side.

#### #74 · `Modules/ProductCatalog/Handlers/CleanupOrphanedProductDataHandler.cs` 🟡 Mark + sweep happen in the **same** invocation
> Zero safety interval defeats the mark/sweep purpose.
> **Fix:** split into two scheduled jobs with a minimum age filter on Phase 2.

#### #75 · `Modules/Webhooks/Services/OutgoingWebhookBackgroundService.cs:91` 🟡 DNS lookup per outbound delivery
> **Fix:** short-TTL `MemoryCache<string,IPAddress[]>` or `SocketsHttpHandler.ConnectCallback`.

#### #76 · `Modules/Webhooks/Security/WebhookSignatureResourceFilter.cs:57` 🟡 Body → string → UTF-8 re-encoding for HMAC
> **Fix:** `HMACSHA256.Create()` + `IncrementalHash` over `request.BodyReader`.

#### #77 · `Modules/Webhooks/Security/HmacHelper.cs:18` 🟡 `$"{timestamp}.{payload}"` then UTF-8 encode allocates ~2× payload
> **Fix:** `IncrementalHash` streaming: timestamp bytes, `.` separator, payload bytes.

#### #78 · `Modules/Notifications/Services/EmailRetryService.cs:68` 🟡 Sequential per-email retry; DB commit per email
> **Fix:** batch DB commits every N successes; parallelise SMTP via pool if server allows.

#### #79 · `Modules/Notifications/Services/FluidEmailTemplateRenderer.cs:18` 🟡 Unbounded static template cache
> Safe today (fixed set), leaks if dynamic names ever slip in.
> **Fix:** validate template name against `EmailTemplateNames` set; or use MemoryCache `SizeLimit`.

#### #80 · `Modules/ProductCatalog/Features/Product/CreateProducts/CreateProductsCommand.cs:64` 🟡 Double-pass validation + entity build duplicates `Price.Create`/`FromPersistence`
> **Fix:** single pass; reuse successful `Price` instance for the entity.

#### #81 · `Modules/Reviews/Features/GetProductReviews/GetProductReviewsByProductIdsQuery.cs:32` 🟡 `request.ProductIds.Distinct().ToDictionary` re-indexes lookup + allocates per missing id
> **Fix:** `lookup.ToDictionary`; TryAdd empty sentinel for missing ids.

#### #82 · `Modules/ProductCatalog/Features/ProductData/GetProductData/GetProductDataQuery.cs:15` 🟡 `GetAllAsync` with no paging
> **Fix:** add paging / cursor parameters.

#### #83 · `Modules/ProductCatalog/Features/TenantCascadeDelete/TenantCascadeDeleteHandler.cs:24` 🟡 Loads all product ids just to bundle them into a cross-module notification
> **Fix:** publish tenant-keyed notification; let Reviews bulk-delete by TenantId.

---
---

## 🔵 CODE STYLE / DRY

#### #84 · `Modules/Chatting/Features/SseController.cs:31` 🔵 `await bus.InvokeAsync` wrapping `IAsyncEnumerable` via `Task.FromResult` is redundant
> **Fix:** resolve dedicated streamer via DI; iterate directly.

#### #85 · `SharedKernel/Application/Events/MessageBusExtensions.cs:13` 🔵 `OutgoingMessagesHelper.Empty` allocates per access
> Name implies singleton; returns `new()` each call.
> **Fix:** cache immutable instance or rename to `CreateEmpty()`.

#### #86 · `Modules/BackgroundJobs/Services/JobProcessingBackgroundService.cs:79` 🔵 Two-source linked CTS where `CancelAfter` suffices
> **Fix:** `CreateLinkedTokenSource(ct).CancelAfter(TimeSpan.FromSeconds(30));`

#### #87 · `Modules/BackgroundJobs/TickerQ/TickerQRecurringJobRegistrar.cs:81` 🔵 Duplicated property assignments across add vs update branches
> **Fix:** extract `ApplyDefinition(entity, definition)` helper.

#### #88 · `Modules/Identity/Directory/Features/User/UpdateUser/UpdateUserCommand.cs:42` 🔵 Inconsistent `var` vs explicit types vs project rule
> **Fix:** explicit types in `AssignUserRolesCommandHandler` to match module style.

#### #89 · `Modules/Identity/Auth/Security/Sessions/BffSessionService.cs:20` 🔵 Implements two interfaces + 7 deps (ISP/SRP)
> **Fix:** split revocation into `BffSessionRevocationService`.

#### #90 · `Modules/Webhooks/Services/WebhookProcessingBackgroundService.cs:29` 🔵 O(n) stringly-typed handler match per message
> **Fix:** `IReadOnlyDictionary<string,IWebhookEventHandler[]>` + wildcard list at startup.

#### #91 · `Modules/Webhooks/Services/OutgoingWebhookBackgroundService.cs:44` 🔵 `using` on factory-created `HttpClient`
> Harmless but misleading.
> **Fix:** drop `using`.

#### #92 · `Modules/Notifications/Repositories/FailedEmailRepository.cs:54` 🔵 `.ToList()` on already-materialised `IReadOnlyList`
> **Fix:** change return type to `IReadOnlyList<T>`; return directly.

#### #93 · `Modules/Notifications/Services/FailedEmailStore.cs:45` 🔵 `catch (Exception ex)` swallows storage errors
> **Fix:** catch specific (`DbUpdateException`, `DbException`); let truly unexpected propagate.

#### #94 · `Modules/ProductCatalog/Repositories/ProductRepository.cs:108` 🔵 `counts?.ToArray() ?? new int[DefaultPriceBuckets.Count]` — simplify fallback
> **Fix:** return `Enumerable.Repeat(0, DefaultPriceBuckets.Count)` or skip the buckets when null.

#### #95 · `Modules/ProductCatalog/GraphQL/Queries/ProductQueries.cs:26` 🔵 12-arg `ProductFilter` build in resolver
> **Fix:** `ProductFilter ToFilter(this ProductQueryInput? input)` extension.

#### #96 · `Modules/ProductCatalog/Features/Product/Shared/ProductValidationHelper.cs:51` 🔵 Selector invoked twice per item
> **Fix:** `items.Select(categoryIdSelector).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();`

#### #97 · `Modules/Reviews/Features/GetProductReviews/GetProductReviewsByProductIdsQuery.cs:20` 🔵 Verbose explicit cast on empty dictionary
> **Fix:** rely on `ErrorOr` implicit conversion; return `new Dictionary<>`.

#### #98 · `Modules/ProductCatalog/Features/Product/CreateProducts/CreateProductsRequest.cs:10` 🔵 `MaxLength(100)` magic literal duplicated across batch requests
> **Fix:** `BatchLimits.MaxItems = 100`.

#### #99 · `Modules/ProductCatalog/Features/Product/UpdateProducts/UpdateProductsCommand.cs:40` 🔵 Indexed `Where((_,i)=>!context.IsFailed(i)).Select(x=>x.Id)` duplicated across handlers
> **Fix:** extract `BatchFailureContext.SuccessfulItems()` / `SuccessfulIndices()`.

#### #100 · `Modules/Notifications/NotificationsModule.cs` 🔵 Commented-out "example controller" line
> **Fix:** delete (YAGNI).

#### #101 · `Modules/ProductCatalog/ProductCatalogModule.cs` 🔵 135-line `AddProductCatalogModule` mixes DI + GraphQL + Mongo + resilience
> **Fix:** extract `AddProductCatalogGraphQL`, `AddProductCatalogMongo`, `AddProductCatalogResilience` (pattern already used by `WebhooksRuntimeBridge`).

> Also noted: ProductCatalog has both `Configuration/` and `Configurations/` folders; two overlapping error files (`DomainErrors.cs` + `ProductCatalogDomainErrors.cs`); `IdempotentCreateCommand` bypasses `Product.Create` factory; Reviews has three global-usings files. Consolidate.

---
---

## 📊 Review Statistics

- **Total findings:** **101** (🔴 6 · 🟠 45 · 🟡 31 · 🔵 19)
- **Scope:** `src/` (excluding `bin/`, `obj/`, `Migrations/`, `StoredProcedures/`, tests)
- **Method:** 6 parallel specialized agents across 2 splits
- **Tests:** excluded per user instruction

### Top Recommended Fixes (order)
1. **Duplicate email sends** on cancellation in `EmailRetryService` (#6)
2. **SSRF TOCTOU + IPv4-mapped IPv6 + HTTP scheme + no replay protection + no rate limit** in Webhooks (#5, #12, #13, #14, #15)
3. **Tenant-bypass bulk deletes** in ProductCatalog & Reviews (#18, #19, #20)
4. **Committed dev secrets** (#7, #8) + unvalidated Mongo options (#9)
5. **CSRF bypass via bearer coexistence** (#29) + `SameSite=Lax` + GET `/logout` (#26, #27)
6. **SMTP STARTTLS** legacy overload (#44) + ROPC password check (#34)
7. **Unsafe file download** (#38, #39) + hardcoded upload cap drift (#37)
8. **Cleanup handlers' mark+sweep in same run** (#74) + progress-commit storm (#51)
9. **Module boundary leak** ProductCatalog→Reviews (#41, #42)

### Files NOT reviewed
(none) ✅ — scope covered all `src/` production modules across both splits.
