# Bug Analysis Report — API-Template-Monolith

> Generated 2026-06-10 by a multi-agent audit (6 parallel auditors: lifetime/request-context, data access, messaging/background jobs, web/API layer, security, domain logic).
> Read-only analysis — **no code was modified**. Findings independently confirmed by more than one auditor are marked **[2x confirmed]**.

## Executive summary

The codebase is generally disciplined (scope hygiene, JWT validation, SSRF protection, HMAC, CSRF are solid), but the audit found **4 critical wiring bugs that make entire background subsystems dead or destructive at runtime**, several request-context bugs where work runs outside the HTTP request scope (tenant filters silently evaluating to `WHERE false`), and a handful of security/config hardening gaps.

**Chained critical failure:** C1 → C2 → C3 together mean: submitted jobs never process, failed emails are never persisted, and the recurring-job scheduler never runs — the entire async/retry pipeline is dead end-to-end. None of this is visible to the current test suite because tests assert only HTTP responses (202 Accepted), never asynchronous outcomes.

| Severity | Count |
|----------|-------|
| Critical | 5 |
| High | 8 |
| Medium | 18 |
| Low | 16 |

---

## CRITICAL

### C1. Non-generic `IUnitOfWork` is never registered — job processing and failed-email persistence are dead at runtime **[2x confirmed]**

- **Files:**
  - `src\Modules\BackgroundJobs\Services\JobProcessingBackgroundService.cs:44,99` — `GetRequiredService<IUnitOfWork>()`
  - `src\Modules\Notifications\Services\FailedEmailStore.cs:45` — same resolution
  - Registration source: `src\BuildingBlocks\BuildingBlocks.Infrastructure.EFCore\Registration\ModuleRegistrationBuilder.cs:96,109` — only `IUnitOfWork<TContext>` and `IUnitOfWork<TMarker>` are registered; bare `IUnitOfWork` has **zero registrations** anywhere in `src` (exhaustive grep).
- **Failure mode:** MS DI does not resolve a base interface from a derived-interface registration. `GetRequiredService<IUnitOfWork>()` throws `InvalidOperationException`:
  - `JobProcessingBackgroundService` crashes on **every dequeued job**; `TryMarkFailedAsync` crashes the same way and the exception is swallowed → every submitted job stays `Pending` forever while the API returns 202 Accepted. The smoke test only asserts 202, so CI never sees it.
  - `FailedEmailStore.StoreFailedAsync` throws inside its own `try`, swallowed by the blanket catch (line 62-65) → retryable failed emails (e.g. tenant invitations, `Retryable: true`) are **silently lost** on SMTP failure.
- **Fix:** Resolve module-specific contracts: `GetRequiredService<IUnitOfWork<BackgroundJobsDbMarker>>()` / `<NotificationsDbMarker>`. Optionally add a startup-time check/test that resolving bare `IUnitOfWork` fails fast.
- **Confidence:** High.

### C2. `IStoredProcedureExecutor` is bound to the wrong module's DbContext — email retry/dead-letter pipeline cannot work **[2x confirmed]**

- **Files:**
  - `src\BuildingBlocks\BuildingBlocks.Infrastructure.EFCore\Registration\ModuleRegistrationBuilder.cs:79-85` — registers a single non-discriminated `IStoredProcedureExecutor` bound to `TContext`.
  - Only **ProductCatalog** calls `AddStoredProcedureSupport()` (`src\Modules\ProductCatalog\ProductCatalogModule.cs:55`), so the container holds exactly one registration: an executor over `ProductCatalogDbContext`.
  - Consumers in other modules: `src\Modules\Notifications\Repositories\FailedEmailRepository.cs:16,20,54,78`, `src\Modules\BackgroundJobs\Services\ReindexService.cs:20,25`.
- **Failure mode:**
  - `FailedEmailRepository.ClaimRetryableBatchAsync/ClaimExpiredBatchAsync` → `ProductCatalogDbContext.Set<FailedEmail>()` → `InvalidOperationException` ("Cannot create a DbSet for 'FailedEmail'") on every run. Email retry and dead-lettering are completely broken (the carefully built `FOR UPDATE SKIP LOCKED` + `xmin` claim functions are unreachable).
  - `ReindexService` runs raw SQL through the *wrong module's* DbContext — works only because both share one physical DB; module-boundary violation that breaks the moment connection strings diverge. If a second module ever calls `AddStoredProcedureSupport()`, resolution becomes last-registration-wins, silently swapping contexts.
- **Fix:** Make the executor module-discriminated (`IStoredProcedureExecutor<TMarker>`, mirroring `IUnitOfWork<TMarker>`) and call `AddStoredProcedureSupport()` in `NotificationsRuntimeBridge` and `BackgroundJobsRuntimeBridge`.
- **Confidence:** High.

### C3. TickerQ scheduler is never started and recurring-job definitions are never seeded — no recurring job ever runs

- **Files:**
  - `src\APITemplate\Api\Program.cs` / `Extensions\Startup\ApplicationBuilderExtensions.cs` — **no `app.UseTickerQ()` call anywhere** (TickerQ requires both `AddTickerQ()` and `app.UseTickerQ()`).
  - `src\Modules\BackgroundJobs\TickerQ\TickerQRecurringJobRegistrar.cs:34` — `SyncAsync` is registered in DI (`BackgroundJobsRuntimeBridge.cs:107`) but **never invoked** by any code path (verified by grep). The `[TickerFunction]` attributes carry no cron expressions, so TickerQ's own attribute seeding cannot substitute.
- **Failure mode:** Cleanup, FTS reindex, email retry, email dead-lettering, orphan-blob sweep, and external sync never execute. Combined with C1/C2, the entire failed-email recovery chain is dead end-to-end.
- **Fix:** Call `app.UseTickerQ()` in the pipeline and invoke `TickerQRecurringJobRegistrar.SyncAsync()` at startup. Note: `UseDatabaseAsync` only runs in Development (`Program.cs:161-162`), so the registrar must not hide behind it for Production.
- **Confidence:** High.

### C4. Tenant global query filter evaluates to `WHERE false` in background scopes — request context used outside the request **[2x confirmed, multiple sites]**

This is the systemic "context outside request" bug class: `HttpRequestIdentityProvider.HasTenant => TenantId != Guid.Empty`, and with no `HttpContext` (background scope created via `IServiceScopeFactory.CreateAsyncScope()`) the snapshot yields `Guid.Empty` → `HasTenant == false` → the tenant filter `HasTenant && entity.TenantId == CurrentTenantId` (`src\BuildingBlocks\BuildingBlocks.Infrastructure.EFCore\Persistence\ModuleDbContext.cs:83`) compiles to `WHERE false` → **every filtered query returns empty**.

**Site A — orphaned-ProductData cleanup mass-deletes *linked* Mongo documents (data loss):**
- **File:** `src\Modules\ProductCatalog\Handlers\CleanupOrphanedProductDataHandler.cs:141-154` — `GetLinkedIdsAsync` queries `dbContext.ProductDataLinks` **without** `IgnoreQueryFilters`. Invoked from the TickerQ job (`src\Modules\BackgroundJobs\Services\CleanupService.cs:68-71`) — no HTTP scope → all links invisible.
- **Failure mode:** Phase 1 marks *every* non-deleted ProductData document older than `RetentionDays` as `PendingDeletion` (its links are invisible). Phase 2 on the next run hard-deletes them (`DeleteManyAsync`, line 114) — **permanent, cross-tenant loss of all product images/videos older than the retention window**, while PG `ProductDataLink` rows still reference them. (Latent until C3 is fixed and the job actually runs — fix before enabling the scheduler.)
- **Fix:** `IgnoreQueryFilters([GlobalQueryFilterNames.Tenant])` in `GetLinkedIdsAsync`, matching the convention in `ProductDataLinkRepository` / `CleanupExpiredInvitationsHandler` (`src\Modules\Identity\Directory\Handlers\CleanupExpiredInvitationsHandler.cs:30`).

**Site B — job consumer cannot load any job:**
- **File:** `src\Modules\BackgroundJobs\Services\JobProcessingBackgroundService.cs:46` — `repo.GetByIdAsync(jobId)` in a background scope. `JobExecution` is `ConfigureTenantAuditable()` (`JobExecutionConfiguration.cs:12`), so even with C1 fixed the lookup returns `null` → handler silently returns → every job stays `Pending`. Same trap in `TryMarkFailedAsync` (line 101).
- **Fix:** Repository method bypassing the tenant filter (`IgnoreQueryFilters` + explicit predicate), or carry `TenantId` in the queue item and set an ambient tenant in the worker scope.

**Site C — orphan-blob refcount always 0 → premature blob deletion (data loss):**
- **File:** `src\Modules\FileStorage\Domain\ActiveStoredFilesBySha256AndTenantSpecification.cs:13`, used by `src\Modules\FileStorage\Features\Delete\MaybeDeleteBlobHandler.cs:22`.
- **Failure mode:** `MaybeDeleteBlobCommand` is processed on a Wolverine worker without HTTP scope. *(Note: this depends on H2 below — today `FileStorageDbContext` doesn't apply global filters at all, so the count works by accident; the moment H2 is fixed, this refcount becomes always 0 and **every** delete removes the physical blob even when other rows reference it.)* The spec already filters `TenantId == tenantId && !IsDeleted` explicitly — the global filters are redundant and harmful here.
- **Fix:** Add `.IgnoreQueryFilters()` to the specification (sibling worker methods `BulkSoftDeleteByProductIdsAsync` etc. already do this).

### C5. HttpClient timeout (`TaskCanceledException`) escapes the consumer loop and stops the whole host — remotely triggerable

- **Files:**
  - `src\BuildingBlocks\BuildingBlocks.Web\InfrastructureBackgroundJobs\Services\QueueConsumerBackgroundService.cs:28` — `catch (Exception ex) when (ex is not OperationCanceledException)`
  - `src\Modules\Webhooks\Services\OutgoingWebhookBackgroundService.cs:60`
- **Failure mode:** `HttpClient` timeout (default 100 s; no explicit timeout configured in `WebhooksRuntimeBridge.cs:55-76`, and the Polly retry strategy does not handle client-side cancellation) throws `TaskCanceledException`, which **is** an `OperationCanceledException` — the filter excludes it from `HandleErrorAsync`, so it propagates out of `ExecuteAsync`. With no `HostOptions.BackgroundServiceExceptionBehavior` override, .NET's default `StopHost` shuts down the entire API. The callback URL is **user-supplied** (`SubmitJobRequest.CallbackUrl`), so anyone with `Examples.Execute` permission can point it at a tar-pit server and take the service down.
- **Fix:** In the base class: `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; } catch (Exception ex) { await HandleErrorAsync(...); }`. Additionally set an explicit per-attempt timeout on the outgoing-webhook HttpClient.
- **Confidence:** High.

---

## HIGH

### H1. Tenant-scoped output-cache eviction silently no-ops — handler runs on a non-request Wolverine thread (stale cache for all tenants) **[confirmed by team]**

- **Files:**
  - `src\APITemplate\Api\Cache\OutputCacheInvalidationService.cs:23-32`
  - `src\APITemplate\Api\Cache\CacheInvalidationHandler.cs:7-14`
  - `src\BuildingBlocks\BuildingBlocks.Infrastructure.Redis\OutputCache\TenantAwareOutputCachePolicy.cs:37-42`
- **Failure mode:** `TenantAwareOutputCachePolicy` rewrites every cache tag to `"{tag}-{tenantId}"` when storing. Invalidation flows as a cascading `CacheInvalidationNotification` (returned from 20+ command handlers, e.g. `PatchProductCommand.cs:81`, `CreateUserCommand.cs:67`). With `UseDurableLocalQueues` (Program.cs:136) these are dispatched on Wolverine's background queue — **no HttpContext**. `OutputCacheInvalidationService` resolves `ITenantProvider` (= `HttpRequestIdentityProvider`), gets `HasTenant == false`, builds an empty suffix, and calls `EvictByTagAsync("products")` — which matches **zero** entries because everything was stored under `"products-{tid}"`. Output cache is never evicted after writes; tenants are served stale products/categories/users/reviews until TTL expiry. Fully silent.
- **Fix:** Capture the tenant at publish time — include `TenantId` in `CacheInvalidationNotification` (set by the publishing handler from its request-scoped `ITenantProvider`) and build the suffixed tag from the event payload.
- **Confidence:** High — full chain verified.

### H2. `FileStorageDbContext` never applies global query filters — cross-tenant download/delete and soft-deleted rows visible (IDOR)

- **File:** `src\Modules\FileStorage\Persistence\FileStorageDbContext.cs:23-28` — `OnModelCreating` does **not** call `ApplyGlobalFilters(modelBuilder)`; every other module context does.
- **Failure mode:**
  - `DeleteFileCommandHandler` (`src\Modules\FileStorage\Features\Delete\DeleteFileCommand.cs:24-27`) loads `StoredFiles.FirstOrDefaultAsync(f => f.Id == id)` with **no tenant and no IsDeleted predicate** → any authenticated user with Upload permission can soft-delete *another tenant's* file by ID, and re-delete soft-deleted rows (re-emitting `MaybeDeleteBlobCommand`).
  - `DownloadFileQueryHandler` (`src\Modules\FileStorage\Features\Download\DownloadFileQuery.cs:17`) similarly serves any tenant's file and soft-deleted files.
- **Fix:** Call `ApplyGlobalFilters(modelBuilder)` (then add `IgnoreQueryFilters` + explicit tenant predicates in saga/background paths — see C4 Site C), or at minimum add `f.TenantId == tenantProvider.TenantId && !f.IsDeleted` predicates to Delete/Download handlers.
- **Confidence:** High.

### H3. Cross-tenant role update & delete — missing tenant scope on mutation path

- **Files:** `src\Modules\Identity\Directory\Features\Role\Shared\RoleByIdSpecification.cs:10`; consumed by `UpdateRoleCommand.cs:32` and `DeleteRoleCommand.cs` (`RoleLoader.LoadMutableAsync`).
- **Failure mode:** `CustomRole` does **not** implement `ITenantEntity`, so the global tenant filter never applies. `RoleByIdSpecification` filters only `r.Id == id`. The read path (`GetRoleQuery`) correctly uses tenant-scoped `RoleByIdForTenantSpecification`, but **Update and Delete do not**. A tenant admin with `Roles.Update`/`Roles.Delete` can modify or delete *another tenant's* custom role by ID. `IsImmutable` only blocks built-in/global roles.
- **Fix:** Load via a tenant-scoped specification on the mutation path too, or compare `TenantId` against `ITenantProvider.TenantId` before allowing mutation.
- **Confidence:** High.

### H4. `[Idempotent]` endpoints have no idempotency at runtime: filter and store are never registered

- **Files:**
  - `src\BuildingBlocks\BuildingBlocks.Web\Api\Filters\Idempotency\IdempotentAttribute.cs:8` (plain `Attribute`, not `IFilterMetadata`)
  - `src\BuildingBlocks\BuildingBlocks.Web\Api\Filters\Idempotency\IdempotencyActionFilter.cs`
  - `src\Modules\ProductCatalog\Features\Product\IdempotentCreate\IdempotentController.Create.cs:14`
- **Failure mode:** `IdempotencyActionFilter` is never added to MVC (`MvcConventionsServiceCollectionExtensions.cs:28` adds only `PaginationFilter`), and `IIdempotencyStore`/`DistributedCacheIdempotencyStore` have **no production DI registration anywhere** (the only registration lives in tests). The `[Idempotent]` attribute is inert metadata — duplicate POSTs with the same `Idempotency-Key` execute the command twice.
- **Fix:** Register `services.Configure<MvcOptions>(o => o.Filters.Add<IdempotencyActionFilter>())` (the filter self-gates on the attribute) plus `AddSingleton<IIdempotencyStore, DistributedCacheIdempotencyStore>` in the Redis path (with a non-Redis fallback).
- **Confidence:** High — exhaustive grep.

### H5. Idempotency cache key is not scoped to tenant, user, or endpoint — cross-tenant response replay (latent until H4 is fixed)

- **Files:** `src\BuildingBlocks\BuildingBlocks.Web\Api\Filters\Idempotency\IdempotencyActionFilter.cs:58-84`, `src\BuildingBlocks\BuildingBlocks.Infrastructure.Redis\Idempotency\DistributedCacheIdempotencyStore.cs:15,38`
- **Failure mode:** The Redis key is `"idempotency:" + <client-supplied Idempotency-Key header>` — nothing else. If user B (any tenant) sends the same key value as user A, the filter short-circuits and replays **user A's cached response body** to user B before the action ever runs. An authenticated attacker can probe key values to read other tenants' creation responses. Not scoped per endpoint either, so future `[Idempotent]` endpoints will cross-replay each other.
- **Fix:** Compose the store key from `tenantId + actorId + HTTP method + route template + clientKey` (hashed). Minor: the two raw-string `BadRequestObjectResult` responses (lines 52, 61) bypass the RFC 7807 contract — return ProblemDetails.
- **Confidence:** High.

### H6. No forwarded-headers handling: IP-keyed rate limiting and HTTPS redirection broken behind any proxy/ingress **[2x confirmed]**

- **Files:** `src\APITemplate\Api\Extensions\RateLimitingServiceCollectionExtensions.cs:185-196` (`GetPartitionKey` → `ctx.Connection.RemoteIpAddress`), `src\APITemplate\Api\Extensions\Startup\ApplicationBuilderExtensions.cs:34` (`UseHttpsRedirection`); `UseForwardedHeaders`/`ForwardedHeadersOptions` appear **nowhere** in `src`. Production config explicitly targets Kubernetes (`appsettings.Production.json5:6`).
- **Failure mode:**
  1. Behind a TLS-terminating ingress/LB, `RemoteIpAddress` is the proxy IP → **all anonymous clients share one token bucket** (`Global.PermitLimit=1000/min`). A single attacker drains it and all anonymous traffic (login/BFF flows, password reset, invitation accept, inbound webhook) gets 429 — trivially triggered DoS. Conversely, per-attacker throttling is ineffective. Client IPs in logs are also wrong.
  2. `Request.Scheme` stays `http` → `UseHttpsRedirection` issues 307 on every request → redirect loop (or `http://` ProblemDetails type URIs via `ErrorOrExtensions.cs:143-147`).
- **Fix:** Add `UseForwardedHeaders` (XFF/XFP) **first** in the pipeline with `KnownProxies`/`KnownNetworks` configured — do not blindly trust XFF (would make the rate-limit key spoofable).
- **Confidence:** High mechanics; severity depends on deployment topology (prod config indicates proxied).

### H7. Production inherits localhost dev CORS origins with `AllowCredentials`

- **Files:** `src\APITemplate\Api\appsettings.json5:39-46` (base), `src\Modules\Identity\IdentityModule.cs:66-97`
- **Failure mode:** CORS is `AllowAnyHeader().AllowAnyMethod().AllowCredentials()` over `Cors.AllowedOrigins`. `appsettings.Production.json5` does **not** override `Cors`, so production trusts `http://localhost:3000`, `http://localhost:5173` (+ https variants) with credentials. Any attacker-controlled app on the victim's machine can make authenticated cross-origin calls and read responses. Also functionally wrong — the real production SPA origin is not allowlisted.
- **Fix:** Set explicit production origins in `appsettings.Production.json5`; remove `localhost` from anything shipping to prod. Consider failing startup if `AllowCredentials` is combined with `localhost`/`*` origins outside Development.

### H8. Outgoing webhooks & background jobs: durable Wolverine message acked into a volatile in-memory channel — at-least-once silently downgraded to at-most-once **[3x confirmed]**

- **Files:**
  - `src\Modules\Webhooks\Features\SendWebhookCallback\SendWebhookCallbackHandler.cs:18-19`
  - `src\Modules\Webhooks\Services\ChannelOutgoingWebhookQueue.cs` (in-memory `Channel`, capacity 500)
  - `src\Modules\Webhooks\Services\OutgoingWebhookBackgroundService.cs:66-74`
  - Same pattern: `src\Modules\BackgroundJobs\Services\ChannelJobQueue.cs` (capacity 100) + `SubmitJobCommand.cs:29-39`, and incoming `WebhookProcessingBackgroundService`.
- **Failure mode:**
  - Webhooks: the durable `SendWebhookCallbackCommand` (Postgres outbox, Program.cs:133-144) is marked processed as soon as the item lands in the channel. Crash/restart loses queued deliveries; after Polly retries (3 attempts) are exhausted, `HandleErrorAsync` only logs — no dead-letter.
  - Jobs: the job row is committed `Pending`, then the ID goes into the in-memory channel. Crash/restart between commit and processing → row stays `Pending` forever; crash mid-processing → stuck `Processing` (no lease/timeout). **No startup or periodic sweep re-enqueues `Pending`/stale `Processing` rows** (verified). With `BoundedChannelFullMode.Wait` and capacity 100, a stalled consumer back-pressures HTTP submits into indefinite waits.
- **Fix:** Perform the HTTP send / job dispatch inside the Wolverine handler (letting durable retry + dead-letter tables do their job) and delete the channel hop; or add a startup/recurring sweep that re-enqueues `Pending` and resets stale `Processing` rows.

---

## MEDIUM

### M1. Cascading messages not atomic with the business commit (outbox gap → lost events) **[2x confirmed]**

- **Files (examples):** `src\Modules\Identity\Directory\Features\User\CreateUser\CreateUserCommand.cs:46,63-68`, `ProvisionKeycloakUserHandler.cs:88-94`, `CreateTenantInvitationCommand.cs:60-78`, `CreateProductsCommand.cs`, `CreateProductReviewCommand.cs`. Root cause: `UnitOfWork.CommitAsync` (`UnitOfWork.cs:47-71`) is plain `SaveChangesAsync`; Wolverine's `UseEntityFrameworkCoreTransactions()` (Program.cs:131) only enrolls handlers that declare a `DbContext` dependency — these declare `IUnitOfWork<TMarker>`, so the transactional/outbox middleware never applies (the team's own comment in `DeleteFileCommand.cs:11-15` documents this).
- **Failure mode:** DB commit and persistence of cascading envelopes (`ProvisionKeycloakUserEvent`, `UserRegisteredNotification`, `CacheInvalidationNotification`, …) happen in separate transactions. A crash in between yields e.g. a committed user with **no Keycloak provisioning event and no recovery**, or an invitation row whose email is never sent.
- **Fix:** Inject the module DbContext (or `IDbContextOutbox<TContext>`) in handlers pairing state change + notification, so envelopes commit in the same transaction; or document at-least-once gaps per handler.
- **Confidence:** Medium-high.

### M2. `IdempotencyActionFilter` uses `RequestAborted` for must-complete work and persists the entry only after the response is written

- **File:** `src\BuildingBlocks\BuildingBlocks.Web\Api\Filters\Idempotency\IdempotencyActionFilter.cs:148-172` (also 102, 108, 130)
- **Failure mode (latent until H4):** `await next()` (response write) runs **before** `_store.SetAsync`. If the client aborts mid-write, the idempotency entry is never stored even though side effects committed — the client's retry re-executes the action. `SetAsync`/`ReleaseAsync` receive `HttpContext.RequestAborted`; an honoring implementation would also fail to release the lock → 409 until lock TTL.
- **Fix:** Store the entry before/independently of writing the response; call `SetAsync`/`ReleaseAsync` with `CancellationToken.None` (point-of-no-return semantics).

### M3. `SubmitJobCommandHandler`: request-aborted token on post-commit enqueue strands jobs as `Pending` forever

- **File:** `src\Modules\BackgroundJobs\Features\SubmitJob\SubmitJobCommand.cs:39-53`
- **Failure mode:** Job row committed, then `jobQueue.EnqueueAsync(entity.Id, ct)` uses the request token (and waits when the channel is full). Abort between commit and enqueue → OCE escapes (`catch ... when (ex is not OperationCanceledException)` deliberately lets it) → compensating delete never runs → job persisted `Pending` but never enters the channel. No reconciliation exists (see H8).
- **Fix:** Enqueue with `CancellationToken.None` after the commit; add the `Pending` sweep from H8.

### M4. `JobProcessingBackgroundService`: shutdown leaves jobs stuck in `Processing` with the recovery path self-cancelled

- **File:** `src\Modules\BackgroundJobs\Services\JobProcessingBackgroundService.cs:39-79,87-119`; base loop `QueueConsumerBackgroundService.cs:20-33`
- **Failure mode:** Job committed `Processing`, then progress steps use the **host stopping token**. On shutdown the OCE propagates and the base class skips `HandleErrorAsync` → nothing marks the job failed. Even when `HandleErrorAsync` runs for other errors during shutdown, `TryMarkFailedAsync` links its 30 s timeout CTS to the already-cancelled token (lines 91-94) → immediate OCE, swallowed (line 115) → job stays `Processing` forever.
- **Fix:** Use a detached timeout token for the "persist terminal state" path; add the stale-job sweep.

### M5. Incoming webhooks: documented EventId dedup does not exist; processing is at-most-once

- **Files:** `src\Modules\Webhooks\Contracts\WebhookPayload.cs:9` ("unique event ID for deduplication"), `WebhooksController.cs:28-35`, `WebhookProcessingBackgroundService.cs:50-58`
- **Failure mode:** (a) A captured request can be replayed for the full 5-minute timestamp tolerance — `EventId` is never checked against any store. (b) Controller returns 200 after writing to an in-memory channel; crash before processing loses the event though the sender saw success. (c) Handler exceptions are logged and the payload dropped — no retry, no DLQ.
- **Fix:** Record `EventId` in a short-TTL idempotency store (Redis `SET NX EX`); persist the payload (Wolverine inbox or DB) before returning 2xx.

### M6. Same HMAC secret for incoming validation and outgoing signing — self-replay loop

- **Files:** `src\Modules\Webhooks\Security\HmacWebhookPayloadSigner.cs:13`, `HmacWebhookPayloadValidator.cs:15` (both read `WebhookOptions.Secret`)
- **Failure mode:** A user submits a job with `CallbackUrl` pointing at this app's own public `/api/v1/webhooks` endpoint. The outgoing delivery is signed with the same secret the incoming validator checks → passes validation, processed as a trusted inbound webhook. The SSRF policy does not block the app's own public address.
- **Fix:** Distinct secrets for inbound verification vs. outbound signing (ideally per-subscriber outbound secrets).

### M7. `Configuration.Sources.Clear()` removes the chained host configuration — `ASPNETCORE_URLS` & co. stop working

- **File:** `src\APITemplate\Api\Program.cs:37`
- **Failure mode:** `Sources.Clear()` deletes the bootstrapped host configuration (memory source + `ASPNETCORE_`/`DOTNET_`-prefixed env vars mapped to host keys like `urls`). `AddEnvironmentVariables()` re-adds only unprefixed vars, so `ASPNETCORE_URLS`/`ASPNETCORE_HTTP_PORTS` silently no longer apply (`--urls` still works via `AddCommandLine`). The documented WebApplicationBuilder gotcha; bites in containerized deployments.
- **Fix:** Don't `Clear()` — remove only the default JSON sources and insert the JSON5 files in their place.

### M8. Nested rate-limiting options are never validated — bad config surfaces as per-request 500s instead of failing at startup

- **Files:** `src\BuildingBlocks\BuildingBlocks.Application\Configuration\ServiceCollectionOptionsExtensions.cs:27`, `src\BuildingBlocks\BuildingBlocks.Web\Http\RateLimitingOptions.cs:31-53`
- **Failure mode:** `ValidateDataAnnotations()` does not recurse into nested complex properties — the `[Range]` attributes on `RateLimitPolicyOptions` are dead; `[Required]` on auto-initialized properties never fires. E.g. `"WindowMinutes": 0` passes `ValidateOnStart`, then the partition factory throws at request time → every request 500s.
- **Fix:** Source-generated `[OptionsValidator]` + `[ValidateObjectMembers]`, or explicit `.Validate(...)` clauses.

### M9. Rate limiter runs after authentication + claims transformation — auth brute force unthrottled

- **File:** `src\APITemplate\Api\Extensions\Startup\ApplicationBuilderExtensions.cs:38-43`
- **Failure mode:** JWT validation, BFF session-store lookups, and `UserPermissionsClaimsTransformation` run **before** `UseRateLimiter`. Forged/expired tokens consume CPU, Keycloak metadata, and session-store I/O without ever being limited (401 challenges never reach the limiter).
- **Fix:** Keep the per-user limiter where it is; add a cheap IP-keyed limiter before `UseAuthentication` as a first line of defense.

### M10. Soft-deleted custom roles leak into queries

- **Files:** `src\Modules\Identity\Persistence\Configurations\CustomRoleConfiguration.cs` (no `HasQueryFilter`); all role specifications.
- **Failure mode:** `CustomRole` is not `ITenantEntity`, so `ApplyGlobalFilters` skips it entirely — including the `!IsDeleted` soft-delete filter. No role specification filters `IsDeleted` either. Soft-deleted roles are returned by `GetRoles`/`GetRole`/assignment lookups; `AssignUserRolesCommandHandler` can re-attach a deleted role; `RoleLoader.LoadMutableAsync` can resurrect one via update.
- **Fix:** `HasQueryFilter(r => !r.IsDeleted)` in `CustomRoleConfiguration`, or `!r.IsDeleted` in every role spec.

### M11. `DeleteProductData` cross-store delete is non-atomic and non-idempotent

- **File:** `src\Modules\ProductCatalog\Features\ProductData\DeleteProductData\DeleteProductDataCommand.cs:43-65`
- **Failure mode:** Step 1 soft-deletes the Mongo document; step 2 soft-deletes PG links in a separate EF transaction. If step 2 fails, products keep active links to deleted data. A retry hits `SoftDeleteAsync == false` (already deleted) → `NotFound` at line 51 → link cleanup is never reached — inconsistency is permanent.
- **Fix:** Invert the order (links inside the EF transaction + durable outbox message for the Mongo update), or make the handler idempotent: when the doc is already soft-deleted, still run link cleanup.

### M12. `MaybeDeleteBlobHandler` refcount check-then-act race deletes a still-referenced blob

- **File:** `src\Modules\FileStorage\Features\Delete\MaybeDeleteBlobHandler.cs:22-39`
- **Failure mode:** Between `CountAsync == 0` and `store.DeleteAsync`, a concurrent commit of the same content hash (CAS dedupe: `FileUploadSaga` promotes the blob *before* inserting the `StoredFile` row) can insert a new reference → physical blob deleted while a committed row points at it → downloads 404. (Interacts with C4 Site C.)
- **Fix:** `pg_advisory_xact_lock` per `(tenant, sha256)` around refcount-check + delete and around promote + insert; or re-verify after delete and compensate.

### M13. Pagination order keys are non-unique — rows can repeat or vanish across pages

- **Files:** `src\BuildingBlocks\BuildingBlocks.Application\Sorting\SortFieldMap.cs:46-63` (single `OrderBy`, no tiebreaker), executed by `PagedQueryExecutor.cs:51-55`
- **Failure mode:** Default sort `Audit.CreatedAtUtc` (batch inserts share timestamps); user sorts `Name`/`Rating`/`Price` — all non-unique. PostgreSQL gives no stable order among ties → `Skip/Take` pages duplicate/omit rows between requests.
- **Fix:** Append a deterministic tiebreaker in `ApplySort` (`.ThenBy(e => e.Id)` / `EF.Property<Guid>(x, "Id")`).

### M14. `CreatedFrom`/`CreatedTo` filters: no UTC normalization against `timestamptz` → 500s

- **Files:** `src\Modules\ProductCatalog\Features\Product\GetProducts\ProductFilterCriteria.cs:53-57`, `src\Modules\Reviews\Features\GetProductReviews\ProductReviewFilterCriteria.cs:33-37`
- **Failure mode:** Query-string values parsed without an offset have `Kind=Unspecified`; Npgsql rejects non-UTC `DateTime` parameters against `timestamp with time zone` → 500 for inputs like `?createdFrom=2026-05-01`. No global converter exists.
- **Fix:** `DateTime.SpecifyKind(value, DateTimeKind.Utc)`/`.ToUniversalTime()` at the boundary.
- **Confidence:** Medium (depends on exact binder per endpoint; GraphQL scalar passes offset-bearing values, REST query-string binding of offset-less values produces `Unspecified`).

### M15. `TenantInvitation` duplicate-pending race — check-then-act without a unique constraint

- **Files:** `CreateTenantInvitationCommand.cs:33` (exists-check), `src\Modules\Identity\Persistence\Configurations\TenantInvitationConfiguration.cs:50` (index on `(TenantId, NormalizedEmail)` is **non-unique**)
- **Failure mode:** Two concurrent invites for the same email both pass `HasPendingInvitationAsync` and both insert → duplicate pending invitations, two valid tokens.
- **Fix:** Partial unique index `(TenantId, NormalizedEmail) WHERE Status = 'Pending'` + translate the unique-violation `DbUpdateException` (pattern already exists: `AppUserUniqueViolationExtensions`).

### M16. TickerQ registrar seeding races on multi-instance startup **[2x confirmed]**

- **File:** `src\Modules\BackgroundJobs\TickerQ\TickerQRecurringJobRegistrar.cs:40-90`
- **Failure mode (latent until C3):** Check-then-insert by fixed Id with no conflict handling — two replicas starting simultaneously both `Add` → PK violation fails one instance's startup.
- **Fix:** Upsert (`ON CONFLICT DO NOTHING`) or catch the unique violation; or a PG advisory lock around the sync.

### M17. Redis leader lease: renewal exception leaves the job running without a lease (split-brain)

- **File:** `src\Modules\BackgroundJobs\TickerQ\RedisDistributedJobCoordinator.cs:139-161`
- **Failure mode:** `executionCts.Cancel()` fires only when the Lua renewal returns 0. If `ScriptEvaluateAsync` *throws* (Redis outage), the renewal loop dies silently, the lease expires after 300 s, another node acquires it — both run the "exclusive" job concurrently.
- **Fix:** try/catch in the renewal body; cancel `executionCts` on renewal failure (or after N consecutive failures).

### M18. Tenant-aware output caching re-enables caching for authenticated responses but varies only by tenant

- **File:** `src\BuildingBlocks\BuildingBlocks.Infrastructure.Redis\OutputCache\TenantAwareOutputCachePolicy.cs:23-43`
- **Failure mode:** The policy overrides the framework's refusal to cache authenticated responses and varies only by `TenantId`. Any endpoint decorated with the policy whose response differs **per user within a tenant** (RBAC-filtered fields, "my …" semantics) will serve user A's body to user B for up to the TTL. Today's decorated endpoints look tenant-uniform — no active leak — but per-user leakage is one attribute away with nothing guarding it.
- **Fix:** Vary by a user discriminator where needed, or enforce (analyzer/checklist) that the policy only decorates strictly tenant-uniform responses.

---

## LOW

### L1. Mongo schema bootstrap drift — `CreateTablesAsync` + swallowed `42P07`
- **File:** `src\BuildingBlocks\BuildingBlocks.Infrastructure.EFCore\Persistence\RelationalDatabaseSchemaExtensions.cs:32-39`
- PostgreSQL aborts the CREATE script at the first duplicate relation; the catch swallows it. New entities/tables added to a model are silently never created in dev DBs (existing tables error first). Mixed strategies (Migrate vs EnsureCreated) on one DB. Dev-only (`Program.cs:161-162`). Fix: create only missing tables or move to real migrations.

### L2. `UnitOfWork.CommitAsync` retries a bare `SaveChangesAsync` under `NpgsqlRetryingExecutionStrategy`
- **File:** `src\BuildingBlocks\BuildingBlocks.Infrastructure.EFCore\UnitOfWork\UnitOfWork.cs:60-70`
- Ambiguous commit (connection dropped after server commit) re-runs `SaveChangesAsync`: `Added` entities re-insert (PK violation), `Modified` hit phantom xmin conflicts. Standard EF caveat; wrap retried saves in a transaction with commit verification, or document.

### L3. `DeleteFileCommandHandler` bypasses audit conventions **[2x confirmed]**
- **File:** `src\Modules\FileStorage\Features\Delete\DeleteFileCommand.cs:31-32`
- Manual `IsDeleted = true; DeletedAtUtc = DateTime.UtcNow` skips `AuditableEntityStateManager.MarkSoftDeleted` → `DeletedBy` stays null; uses `DateTime.UtcNow` instead of `TimeProvider` (only such spot in the module). Fix: delete via `IStoredFileRepository.DeleteAsync`.

### L4. GraphQL N+1 on `CategoryType.products`
- **File:** `src\Modules\ProductCatalog\GraphQL\Types\CategoryTypeResolvers.cs:20-37`
- One `GetProductsQuery` (≈4 SQL queries) **per category** in a list; no DataLoader, unlike `CategoryByIdDataLoader`/`ProductReviewsByProductDataLoader`. Scalability, not correctness.

### L5. GraphQL `Product.reviews` field is unbounded — no pagination or row cap
- **Files:** `src\Modules\ProductCatalog\GraphQL\Types\ProductTypeResolvers.cs:18-25`, `GetProductReviewsByProductIdsQueryHandler.cs:23-30`
- `products(input: {pageSize: 100}) { reviews { … } }` loads **all** reviews for up to MaxPageSize products (spec has no limit). Depth/cost limits are static and don't bound row counts. Fix: cap per-product reviews in the spec or use HotChocolate paging.

### L6. BFF session cache repopulation race after bulk revoke
- **File:** `src\Modules\Identity\Auth\Security\Sessions\BffPostgresSessionStoreBase.cs:204-219`
- `ExecuteUpdateAsync` then cache `RemoveAsync`; a concurrent `GetAsync` that read pre-UPDATE can re-cache the stale "active" record after the remove → revoked session usable until sliding TTL. Fix: double-delete, version-check, or version in the cache key.

### L7. `TenantCascadeDeleteHandler` snapshot race
- **File:** `src\Modules\ProductCatalog\Features\TenantCascadeDelete\TenantCascadeDeleteHandler.cs:25-28`
- `productIds` read *before* the bulk-delete transaction; products created in between are soft-deleted but excluded from `ProductsBatchSoftDeletedNotification` → Reviews never cascades for them. Fix: read IDs inside the transaction (or `RETURNING`).

### L8. Job completion callback can be lost between commit and send; no idempotency key on deliveries
- **File:** `src\Modules\BackgroundJobs\Services\JobProcessingBackgroundService.cs:76-78,121-144`
- `MarkCompleted` committed, webhook command sent afterwards in a separate scope/transaction — crash in between = callback never delivered. Dead `OutgoingJobWebhookPayload` record vs anonymous object actually serialized (contract drift). Fix: outbox in the same transaction; `X-Webhook-Event-Id` header; serialize the real type.

### L9. Email retry: duplicate sends on crash, contrary to the code comment
- **File:** `src\Modules\Notifications\Services\EmailRetryService.cs:80-110` (comment at 106)
- SMTP succeeds, crash before `CommitAsync` → lease expires → email sent again. Inherently at-least-once; the comment overpromises. Fix: document, or stage a "Sending" state committed before the SMTP call.

### L10. Graceful shutdown drops everything buffered in every `BoundedChannelQueue`
- **File:** `src\BuildingBlocks\BuildingBlocks.Web\InfrastructureBackgroundJobs\Services\BoundedChannelQueue.cs` + `QueueConsumerBackgroundService.cs:22`
- Nothing calls `Writer.Complete()`; on SIGTERM all buffered items are discarded even on a clean deploy. Fix: complete + drain with a grace period (or replace channels with durable queues per H8).

### L11. Welcome/role-change emails silently dropped on SMTP failure by design defaults
- **Files:** `src\Modules\Notifications\Contracts\EmailMessage.cs:28` (`Retryable = false` default), `SendEmailMessageHandler.cs:28-32`
- Handler swallows every non-cancellation exception, so Wolverine retry/DLQ never sees email failures; only tenant invitations opt into `Retryable`. Related: `FailedEmailStore` is called with the message-processing token — on shutdown mid-store the OCE is swallowed, handler "succeeds", Wolverine acks → email permanently lost (`FailedEmailStore.cs:40-65`, `SendEmailMessageHandler.cs:31`). Fix: `CancellationToken.None` for the failed-email persistence; flip `Retryable` default or propagate exceptions for retryable templates.

### L12. SSRF policy gaps in `DefaultNetworkSecurityPolicy`
- **File:** `src\Modules\Webhooks\Security\DefaultNetworkSecurityPolicy.cs:42-57`
- `0.0.0.0/8` (only exact `0.0.0.0` blocked), `192.0.0.0/24`, `198.18.0.0/15`, multicast `224.0.0.0/4`, `255.255.255.255` allowed; `http://` scheme permitted for callbacks. The TOCTOU-safe `ConnectCallback` pinning is otherwise solid. Fix: add remaining IANA special-purpose blocks; HTTPS-only outside Development.

### L13. Callback URLs logged verbatim
- **File:** `src\Modules\Webhooks\Services\OutgoingWebhookBackgroundService.cs:63,72`
- Callback URLs often embed capability tokens in query strings; they hit logs on every success/failure. Redact query strings.

### L14. Committed dev secrets & defaults
- `infrastructure/keycloak/realms/api-template-realm.json` — hardcoded client secrets (`dev-client-secret`, `dev-password-verification-secret`), `sslRequired: none`, seeded admin/admin. Dev-scoped today (prod compose uses `start --optimized` without `--import-realm`), but it's the obvious template an operator would reuse. Sanitize with placeholders.
- `src\APITemplate\Api\appsettings.json5:25-27` — base `ConnectionStrings:DefaultConnection` defaults to `postgres/postgres`; if the prod env var is absent/misnamed, the app falls back to superuser credentials instead of failing closed. Leave the base empty and fail fast.
- `src\APITemplate\Api\appsettings.Development.json5:51` — committed redaction HMAC key (dev-only; prod forces env var). Move to user-secrets/local.json5.
- `docker-compose.production.yml:171-185` — no `Webhook__Secret`; `ValidateOnStart` will fail boot until added (fail-closed, but the documented prod path won't start).

### L15. Misc web-layer polish
- `src\APITemplate\Api\Middleware\RequestSizeLimitsMiddleware.cs:49-53` — 413 as plain text (breaks RFC 7807 contract); chunked bodies (null `ContentLength`) skip the proactive check (unbounded in TestServer).
- `src\APITemplate\Api\appsettings.json5:5-9` vs `Program.cs:38-55` — documented config precedence contradicts code (`local.json5` added **before** env vars, so env vars win; in Development user secrets override env vars, reverse of framework default).
- `src\APITemplate\Api\ExceptionHandling\ApiExceptionHandler.cs:52-53` — `IHasErrorMetadata.Metadata` copied into 500 ProblemDetails (open channel for internals from Keycloak/SMTP exceptions). Whitelist keys or log-only on ≥500.
- `src\APITemplate\Api\ExceptionHandling\ApiExceptionHandler.cs:30` — GraphQL bypass keyed on hardcoded `/graphql` path; derive from a shared constant.
- `src\APITemplate\Api\Extensions\HstsServiceCollectionExtensions.cs:26-27` — dead `AddHsts` registration (`UseHsts()` never called); `SecurityHeadersExtensions.cs:40-47` — `Preload=true` branch hardcodes `includeSubDomains` regardless of config.
- `src\APITemplate\Api\Middleware\RequestCorrelationHelper.cs:10-16` — client-supplied correlation id reflected unvalidated (length/charset) — log-forgery vector; cap and sanitize.
- `src\Modules\ProductCatalog\GraphQL\Mutations\ProductMutations.cs:44-45` — raw `GraphQLException` with concatenated internal failure strings; map through `ErrorOrGraphQLExtensions`.
- `src\APITemplate\Api\Extensions\Startup\ApplicationBuilderExtensions.cs:91-97` — root `/` status endpoint requires auth (fallback policy, no `.AllowAnonymous()`) → 401 for LB/status pages.

### L16. Misc domain/lifetime polish
- `src\Modules\Reviews\Domain\ProductReview.cs:8-13` — aggregate with fully public setters; `Rating` assignable as `default` (Value == 0, outside 1–5), bypassing `Rating.Create`. Make setters private/init.
- `src\Modules\ProductCatalog\Entities\Product.cs:38`, `AppUser.cs:52`, `CustomRole.cs:34-35` — mutable `ICollection` with public get/set, bypassing `SyncProductDataLinks`/`SetPermissions`. Expose `IReadOnlyCollection` backed by private fields.
- `InvitationStatus.Expired` is a dead enum state — never assigned; expiry is date-computed. Pick one source of truth.
- `src\Modules\ProductCatalog\Repositories\ProductRepository.cs:16-21,108-114` — price-facet labels (`"0 - 50"`) imply inclusive bounds but buckets are half-open `[0,50)`. Cosmetic.
- `src\Modules\Identity\Auth\Security\Sessions\BffSessionRevocationSubscriber.cs:37,79,102-105` — fire-and-forget reconnect handler; `_subscribeGate.WaitAsync()` outside the try → possible unobserved `ObjectDisposedException` racing `Dispose()` at shutdown.
- `src\Modules\Chatting\Features\GetNotificationStream\GetNotificationStreamQuery.cs:9-16` + `SseController.cs:31-41` — `IAsyncEnumerable` returned by a Wolverine handler is enumerated after the handler scope is disposed. Safe today (captures only `TimeProvider`), but a use-after-scope-dispose trap the moment anyone injects a repository. Add a guard comment or restructure.
- `src\Modules\BackgroundJobs\BackgroundJobsRuntimeBridge.cs:145` — TickerQ `MaxConcurrency = 1` serializes all recurring jobs per node (long cleanup delays email retry). Intentional? Comment or per-function concurrency.
- `src\Modules\BackgroundJobs\TickerQ\Jobs\EmailRetryRecurringJob.cs:25,29` — hardcoded function name instead of `TickerQFunctionNames.EmailRetry`; drift risk.
- `src\Modules\FileStorage\Domain\Sagas\FileUploadSaga.cs:159-166` — commit reply built from `entity.Audit.CreatedAtUtc` **before** `SaveChangesAsync` (audit stamping happens inside SaveChanges) → v2 commit returns `0001-01-01T00:00:00Z`.
- `src\Modules\FileStorage\Domain\Sagas\FileUploadSaga.cs` (`Handle(CommitUploadCommand)`) — commit keyed only by the opaque `uploadToken`; caller's tenant never compared to `saga.TenantId`. Mitigated by 128-bit random token + short TTL. Assert tenant match before promoting.
- `src\Modules\Webhooks\Security\HmacWebhookPayloadValidator.cs:23-29` — inbound webhook replay window (±300 s) with no nonce/dedup (overlaps M5).

---

## Areas verified clean (checked, deliberately not flagged)

- **Captive dependencies:** none found — all `AddSingleton` registrations across modules and host depend only on singleton-safe services.
- **Background scope hygiene:** background services consistently use `IServiceScopeFactory.CreateAsyncScope`; `ScopedBffSessionDbContextFactory` leases properly disposed; TickerQ jobs scoped per execution. `InProcessBffRefreshCore`'s `Task.Run` leader is always awaited inline; `RedisBffRefreshCoordinator` deliberately decouples from `RequestAborted` (documented).
- **JWT validation** complete (issuer/audience/lifetime/signing key; RS256 via discovery; `RequireHttpsMetadata` outside HTTP authorities). Refresh-token rotation/reuse detection delegated to Keycloak (`revokeRefreshToken:true`, `refreshTokenMaxReuse:0`).
- **Tenancy/IDOR elsewhere:** EF global filters apply tenant + soft-delete in Identity/Reviews/Notifications/BackgroundJobs/ProductCatalog contexts; Mongo `ProductDataRepository` filters tenant explicitly; Reviews delete enforces author ownership. (Exceptions: H2, H3.)
- **FileStorage hardening:** path-traversal guard, streaming size cap, extension + content-type allow-lists, forced `attachment` downloads, `nosniff`.
- **Crypto:** `RandomNumberGenerator`/GUIDs for tokens; SHA-256 invitation-token hashing; HMAC-SHA256 + `FixedTimeEquals`; no MD5/SHA1/ECB/static IVs/`System.Random`.
- **Injection:** only parameterized `FromSqlInterpolated`; one `ExecuteSqlRawAsync` with no user input; typed Mongo `Builders`; Fluid templating auto-encodes.
- **GraphQL hardening:** depth 5, cost analyzer, introspection off outside Development, `[Authorize]` on all root types, fallback HTTP auth policy covers `/graphql`.
- **Redis:** `IConnectionMultiplexer` proper singleton; idempotency/lock/lease code uses atomic Lua compare-and-set with TTLs; RedisRateLimiting key prefixes verified distinct (decompiled 1.2.1).
- **Webhook HMAC** (timestamp-bound, constant-time compare) and the dual-layer 1 MB size guard on the receiver.
- **`MailKitEmailSender`** serializes the non-thread-safe `SmtpClient` behind a semaphore; `UnitOfWork` savepoint/snapshot-restore logic correct; `JobExecution`/`FailedEmail`/`FileUploadSaga`/`TenantInvitation`/`BffSessionValidator` state machines and expiry operators correct; no `DateTime.Now` (local time) anywhere.

## Suggested fix order

1. **C4 Site A** (`CleanupOrphanedProductDataHandler`) — data-loss bug; must be fixed **before** C3 ever enables the scheduler.
2. **C1 + C2** — unblock job processing and the email retry pipeline (mechanical DI fixes).
3. **C3** — start TickerQ + seed recurring jobs (after 1 & 2).
4. **C5** — host-stopping DoS via user-supplied callback URL.
5. **H2 + C4 Site C together** — FileStorage filters + `IgnoreQueryFilters` on the blob refcount spec (fixing H2 alone activates the C4-C data-loss path).
6. **H1, H3, H4+H5, H6, H7** — stale cache, cross-tenant role mutation, idempotency, forwarded headers, CORS.
7. Mediums in listed order; Lows opportunistically.

## Testing gap (root cause of invisibility)

All five critical bugs are invisible to CI because tests assert only synchronous HTTP responses. A single integration test that submits a job and polls until `Completed` would have caught C1, C3, H8, and M3/M4 at once. Recommended: async-outcome integration tests (job completion, email retry after induced SMTP failure, cache eviction after mutation, webhook delivery after restart).
