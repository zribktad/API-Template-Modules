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
- [ ] Create 4 domain-driven projects
- [ ] Setup `NotificationsDbContext` (`FailedEmail`)
- [ ] Migrate Email infrastructure (Queue, Sender, Templates, Retry Service)
- [ ] Verify `dotnet build` & tests

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
- [ ] Setup `BackgroundJobs.Application` (Job features, options)
- [ ] Setup `BackgroundJobs.Infrastructure` (`BackgroundJobsDbContext`, `JobExecutionRepository`, `JobExecutionConfiguration`)
- [ ] Setup `TickerQ` and `Services` in `BackgroundJobs.Infrastructure`
- [ ] Setup `BackgroundJobs.Api` (`JobsController`, `BackgroundJobsModule.cs`)
- [ ] Set up module references and wiring in `Program.cs`
- [ ] Verify `dotnet build` & tests

## Unit 7: Webhooks Module
- [ ] Create 4 domain-driven projects (in-memory queues, no EF)
- [ ] Migrate HMSC validator, signer, and webhook jobs
- [ ] Verify `dotnet build` & tests

## Unit 8: Host Rewiring + Cleanup
- [x] Wire up `Program.cs` extension methods
- [x] Update `APITemplate.Api.csproj` references
- [ ] Delete legacy `APITemplate.Domain`, `APITemplate.Application`, `APITemplate.Infrastructure` (except Migrations)
- [x] Final verification (`dotnet build`, `dotnet test`)
