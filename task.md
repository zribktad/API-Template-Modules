# Modular Monolith Migration Tasks

## Unit 0: Foundation
- [x] Create `SharedKernel` project
- [x] Create `Contracts` project
- [x] Extract core `APITemplate.Domain` entities to `SharedKernel`
- [x] Update `APITemplate.Domain` legacy references to point to `SharedKernel`
- [x] Finish `SharedKernel` alignment with the agreed Unit 0 scope from `implementation_plan.md`
  - [x] Move Application layer components (Context, DTOs, Batch, Sorting, Validation, Errors, Search, Http, Middleware, Options)
  - [x] Move remaining planned Application components (`Resilience`, `Startup`)
  - [x] Add `IUnitOfWork<TContext>` to `SharedKernel.Domain.Interfaces`
  - [x] Move/refactor planned `SharedKernel.Infrastructure` components (`ModuleDbContext`, generalized `RepositoryBase`, `UnitOfWork<TContext>`, `SoftDelete`, configuration/registration utilities)
  - [x] Move configuration/resilience helpers from `APITemplate.Api` to `SharedKernel.Infrastructure`
  - [x] Move `AddQueueWithConsumer<>` registration to `SharedKernel.Infrastructure`
  - [x] Replace legacy compatibility imports with explicit namespace shims / wrappers for moved types
  - [x] Configure `Contracts.csproj` (reference SharedKernel)
  - [x] Move existing integration event records (Cache invalidation, Email, SoftDelete)
  - [x] Keep `Contracts` event-only; leave `CacheTags` and `MessageBusExtensions` public in `SharedKernel`
  - [x] Define new `ProductSoftDeletedNotification` and extend `TenantSoftDeletedNotification`
- [x] Add both to `APITemplate.slnx` and add references to legacy projects
- [x] Ensure full solution compiles (`dotnet build`)
- [x] Run targeted Unit 0 regression tests (`UnitOfWork`, configuration/options, startup coordination, queues/webhooks, soft-delete`)

## Unit 1: ProductCatalog Module
- [x] Create 4 projects (`ProductCatalog.Domain`, `ProductCatalog.Application`, `ProductCatalog.Infrastructure`, `ProductCatalog.Api`)
- [x] Migrate Domain entities & repos (`Product`, `Category`, `ProductDataLink`, etc.)
- [x] Migrate Application features (`Features/Product`, `Features/Category`, etc.)
- [x] Migrate Infrastructure (`ProductCatalogDbContext`, MongoDB, configs, cascade rules)
- [x] Migrate API & GraphQL
- [x] Verify `dotnet build` & tests

## Unit 2: Reviews Module
- [x] Create 4 domain-driven projects
- [x] Setup `ReviewsDbContext` and ProductReview entity
- [x] Implement `ProductSoftDeletedEventHandler`
- [x] Verify `dotnet build` & tests

## Unit 3: Identity Module
- [x] Create 4 domain-driven projects
- [x] Setup `IdentityDbContext` (`AppUser`, `Tenant`, `TenantInvitation`)
- [x] Migrate Auth infrastructure (Keycloak, BFF, Providers)
- [x] Migrate Controllers & Authorization
- [x] Verify `dotnet build` & tests

## Unit 4: Notifications Module
- [x] Create 4 domain-driven projects (`Notifications.Domain`, `Notifications.Application`, `Notifications.Infrastructure`, `Notifications.Api`)
- [x] Setup `NotificationsDbContext` (`FailedEmail`) with `FailedEmailConfiguration`
- [x] Setup `Notifications.Domain` (`FailedEmail` entity, `IFailedEmailRepository`)
- [x] Setup `Notifications.Application` (Email contracts: `IEmailSender`, `IEmailQueue`, `IEmailTemplateRenderer`, `IEmailRetryService`, `IFailedEmailStore`, `EmailMessage`, `EmailOptions`, `EmailTemplateNames`)
- [x] Setup `Notifications.Application` Handlers (`UserRegisteredEmailHandler`, `TenantInvitationEmailHandler`, `UserRoleChangedEmailHandler`)
- [x] Migrate Email infrastructure (`MailKitEmailSender`, `ChannelEmailQueue`, `FluidEmailTemplateRenderer`, `EmailSendingBackgroundService`, `FailedEmailStore`, `FailedEmailErrorNormalizer`)
- [x] Migrate Email retry infrastructure (`EmailRetryService`, `EmailRetryRecurringJob`, `EmailRetryRecurringJobRegistration`)
- [x] Setup stored procedures (`ClaimRetryableFailedEmailsProcedure`, `ClaimExpiredFailedEmailsProcedure`) + SQL migrations
- [x] Setup Liquid email templates (`user-registration`, `tenant-invitation`, `user-role-changed`)
- [x] Setup `FailedEmailRepository` with stored procedure integration
- [x] Setup `NotificationsRuntimeBridge` (DI registration: DbContext, UoW, repos, email queue, SMTP resilience pipeline)
- [x] Setup `NotificationsModule.cs` (event-driven, no REST controllers)
- [x] Add all 4 projects to `APITemplate.slnx`
- [x] Verify `dotnet build` — 0 errors, 0 warnings
- [x] Verify `dotnet test` — 49/49 passed

## Unit 5: FileStorage Module
- [x] Create 4 domain-driven projects for FileStorage
- [x] Setup `FileStorage.Domain` (`StoredFile`, `IStoredFileRepository`)
- [x] Setup `FileStorage.Application` (File features, options, etc.)
- [x] Setup `FileStorage.Infrastructure` (`FileStorageDbContext`, `StoredFileRepository`, `StoredFileConfiguration`, `LocalFileStorageService`)
- [x] Setup `FileStorage.Api` (`FilesController`, `FileStorageModule.cs`)
- [x] Set up module references and wiring in `Program.cs`
- [x] Verify `dotnet build` & tests

## Unit 6: BackgroundJobs Module
- [x] Create 4 domain-driven projects for BackgroundJobs
- [x] Setup `BackgroundJobs.Domain` (`JobExecution`, `JobStatus`, `IJobExecutionRepository`)
- [x] Setup `BackgroundJobs.Application`
  - [x] DTOs: `SubmitJobRequest`, `GetJobStatusRequest`, `JobStatusResponse`
  - [x] Commands: `SubmitJobCommand` + handler (create entity, enqueue, mark Failed on enqueue error)
  - [x] Queries: `GetJobStatusQuery` + handler (find by Id, return NotFound error if null)
  - [x] Mappings: `JobMappings.ToResponse()`
  - [x] Services interfaces: `IJobQueue`, `IJobQueueReader`, `ICleanupService`, `IReindexService`, `IExternalIntegrationSyncService`
  - [x] Options: `BackgroundJobsOptions` (TickerQ, Cleanup, Reindex, ExternalSync crons, retention, batch sizes)
- [x] Setup `BackgroundJobs.Infrastructure` persistence
  - [x] `BackgroundJobsDbContext` (inherits `ModuleDbContext`, `DbSet<JobExecution>`)
  - [x] `JobExecutionConfiguration` (key, `ConfigureTenantAuditable`, Status/JobType/ProgressPercent constraints, indexes)
  - [x] `JobExecutionRepository` (inherits `RepositoryBase<JobExecution>`)
- [x] Setup Services in `BackgroundJobs.Infrastructure`
  - [x] `ChannelJobQueue` (bounded channel capacity 100, implements `IJobQueue` + `IJobQueueReader`)
  - [x] `JobProcessingBackgroundService` (dequeue → MarkProcessing → simulate work → MarkCompleted/MarkFailed → webhook callback)
  - [x] `CleanupService` (Wolverine cross-module commands + `ISoftDeleteCleanupStrategy` collection)
  - [x] `ReindexService` (FTS index bloat check via stored procs, REINDEX CONCURRENTLY if >30% bloat)
  - [x] `ExternalIntegrationSyncServicePreview` (placeholder, logs info)
  - [x] `SoftDeleteCleanupStrategy<TEntity>` (generic batch hard-delete via `ExecuteDeleteAsync`)
  - [x] `GetFtsIndexNamesProcedure` + `GetIndexBloatPercentProcedure` (stored procedure wrappers)
- [x] Setup TickerQ in `BackgroundJobs.Infrastructure`
  - [x] `TickerQSchedulerDbContext` (inherits `TickerQDbContext`, schema "tq_scheduler")
  - [x] `TickerQRecurringJobRegistrar` (syncs cron job definitions to DB via `IRecurringBackgroundJobRegistration`)
  - [x] `CleanupRecurringJob` (`[TickerFunction("cleanup-recurring-job")]`)
  - [x] `ReindexRecurringJob` (`[TickerFunction("reindex-recurring-job")]`)
  - [x] `ExternalSyncRecurringJob` (`[TickerFunction("external-sync-recurring-job")]`)
  - [x] `CleanupRecurringJobRegistration`, `ReindexRecurringJobRegistration`, `ExternalSyncRecurringJobRegistration`
  - [x] `DragonflyDistributedJobCoordinator` (Redis Lua lease management, 5-min lease, renewal ~100s)
- [x] Setup Validation in `BackgroundJobs.Infrastructure`
  - [x] `BackgroundJobsOptionsValidator` (CRON syntax, InstanceNamePrefix, CoordinationConnection, batch sizes)
- [x] Setup `BackgroundJobsRuntimeBridge` (DI: DbContext, UoW, repo, queue consumer, services, TickerQ conditional)
- [x] Setup `BackgroundJobs.Api`
  - [x] `JobsController`: `POST /jobs` → `SubmitJobCommand` (202 Accepted + Location), `GET /jobs/{id}` → `GetJobStatusQuery`
  - [x] `[RequirePermission(Permission.Examples.Execute)]` on Submit
  - [x] `[RequirePermission(Permission.Examples.Read)]` on GetStatus
  - [x] `BackgroundJobsModule.cs`: `AddBackgroundJobsModule()` + `MapBackgroundJobsEndpoints()`
- [x] Add all 4 projects to `APITemplate.slnx`
- [x] Set up module references and wiring in `Program.cs`
- [x] Verify `dotnet build` — 0 errors, 0 warnings
- [x] Verify `dotnet test` — 49/49 passed

## Unit 7: Webhooks Module
- [x] Create 4 domain-driven projects (in-memory queues, no EF)
- [x] Migrate HMAC validator, signer, and webhook jobs
- [x] Verify `dotnet build` & tests — 0 errors, 49/49 passed

## Unit 8: Host Rewiring + Cleanup
- [x] Wire up `Program.cs` extension methods
- [x] Update `APITemplate.csproj` references
- [x] Delete legacy `APITemplate.Domain`, `APITemplate.Application`, `APITemplate.Infrastructure` (already removed)
- [x] Final verification (`dotnet build`, `dotnet test`)
