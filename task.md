# Modular Monolith Migration Tasks

## Unit 0: Foundation
- [x] Create `SharedKernel` project
- [x] Create `Contracts` project
- [x] Extract core `APITemplate.Domain` entities to `SharedKernel`
- [x] Update `APITemplate.Domain` legacy references to point to `SharedKernel`
- [x] Fix module references and missing usings in `SharedKernel`
  - [x] Move Application layer components (Context, DTOs, Batch, Sorting, Validation, Errors, Search, Http, Middleware, Options)
  - [x] Refactor & generalize Infrastructure components (ModuleDbContext, SoftDelete with `DbContext`, UnitOfWork with `DbContext`, Registration extensions)
  - [x] Set up `global using` re-exports in old legacy projects
- [x] Create `Contracts` project
  - [x] Configure `Contracts.csproj` (reference SharedKernel)
  - [x] Move existing Integration Events (Cache, Email, SoftDelete)
  - [ ] Define new `ProductSoftDeletedNotification` and extend `TenantSoftDeletedNotification`
- [ ] Add both to `APITemplate.slnx` and add references to legacy projects
- [ ] Ensure full solution compiles (`dotnet build`)

## Unit 1: ProductCatalog Module
- [ ] Create 4 projects (`ProductCatalog.Domain`, `ProductCatalog.Application`, `ProductCatalog.Infrastructure`, `ProductCatalog.Api`)
- [ ] Migrate Domain entities & repos (`Product`, `Category`, `ProductDataLink`, etc.)
- [ ] Migrate Application features (`Features/Product`, `Features/Category`, etc.)
- [ ] Migrate Infrastructure (`ProductCatalogDbContext`, MongoDB, configs, cascade rules)
- [ ] Migrate API & GraphQL
- [ ] Verify `dotnet build` & tests

## Unit 2: Reviews Module
- [ ] Create 4 domain-driven projects
- [ ] Setup `ReviewsDbContext` and ProductReview entity
- [ ] Implement `ProductSoftDeletedEventHandler`
- [ ] Verify `dotnet build` & tests

## Unit 3: Identity Module
- [ ] Create 4 domain-driven projects
- [ ] Setup `IdentityDbContext` (`AppUser`, `Tenant`, `TenantInvitation`)
- [ ] Migrate Auth infrastructure (Keycloak, BFF, Providers)
- [ ] Migrate Controllers & Authorization
- [ ] Verify `dotnet build` & tests

## Unit 4: Notifications Module
- [ ] Create 4 domain-driven projects
- [ ] Setup `NotificationsDbContext` (`FailedEmail`)
- [ ] Migrate Email infrastructure (Queue, Sender, Templates, Retry Service)
- [ ] Verify `dotnet build` & tests

## Unit 5: FileStorage Module
- [ ] Create 4 domain-driven projects
- [ ] Setup `FileStorageDbContext` (`StoredFile`)
- [ ] Migrate `LocalFileStorageService`
- [ ] Verify `dotnet build` & tests

## Unit 6: BackgroundJobs Module
- [ ] Create 4 domain-driven projects
- [ ] Setup `BackgroundJobsDbContext` (`JobExecution`)
- [ ] Migrate TickerQ infrastructure & job queue
- [ ] Verify `dotnet build` & tests

## Unit 7: Webhooks Module
- [ ] Create 4 domain-driven projects (in-memory queues, no EF)
- [ ] Migrate HMSC validator, signer, and webhook jobs
- [ ] Verify `dotnet build` & tests

## Unit 8: Host Rewiring + Cleanup
- [ ] Wire up `Program.cs` extension methods
- [ ] Update `APITemplate.Api.csproj` references
- [ ] Delete legacy `APITemplate.Domain`, `APITemplate.Application`, `APITemplate.Infrastructure` (except Migrations)
- [ ] Final verification (`dotnet build`, `dotnet test`)
