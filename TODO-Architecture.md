# Architecture Evolution: Monolith to Microservices

## Current State

Well-structured **Clean Architecture monolith** with:
- Vertical slice features (Product, Category, User, Tenant, Email, Webhooks, Jobs)
- Wolverine as message bus (supports distributed transport)
- Dual database (PostgreSQL + MongoDB)
- Multi-tenancy with isolated query filters
- Domain events for cross-feature communication

---

## Identified Bounded Contexts

| # | Service | Entities | Database |
|---|---------|----------|----------|
| 1 | **Product Catalog** | Product, Category, ProductData, ProductDataLink, ProductCategoryStats | PostgreSQL + MongoDB |
| 2 | **Reviews** | ProductReview | PostgreSQL |
| 3 | **Identity & Tenancy** | AppUser, Tenant, TenantInvitation | PostgreSQL + Keycloak |
| 4 | **Notifications** | FailedEmail, email templates | PostgreSQL |
| 5 | **File Storage** | StoredFile | PostgreSQL + filesystem/S3 |
| 6 | **Background Jobs** | JobExecution | PostgreSQL (TickerQ) |
| 7 | **Webhooks** | Incoming/Outgoing webhooks | In-memory queues |

---

## Recommended Strategy: Modular Monolith → Strangler Fig

### Phase 1 — Modular Monolith

Transform the current monolith into isolated modules with explicit boundaries while keeping everything in a single deployable unit.

#### Step 1: Create Module Structure

Create a new directory layout under `src/Modules/`:

```
src/Modules/
  ProductCatalog/
    ProductCatalog.Domain/
    ProductCatalog.Application/
    ProductCatalog.Infrastructure/
    ProductCatalog.Api/
  Reviews/
    Reviews.Domain/
    Reviews.Application/
    Reviews.Infrastructure/
    Reviews.Api/
  Identity/
    Identity.Domain/
    Identity.Application/
    Identity.Infrastructure/
    Identity.Api/
  Notifications/
    Notifications.Domain/
    Notifications.Application/
    Notifications.Infrastructure/
    Notifications.Api/
  FileStorage/
    FileStorage.Domain/
    FileStorage.Application/
    FileStorage.Infrastructure/
    FileStorage.Api/
  BackgroundJobs/
    BackgroundJobs.Domain/
    BackgroundJobs.Application/
    BackgroundJobs.Infrastructure/
    BackgroundJobs.Api/
  Webhooks/
    Webhooks.Domain/
    Webhooks.Application/
    Webhooks.Infrastructure/
    Webhooks.Api/
```

#### Step 2: Extract Shared Kernel

Create `src/SharedKernel/` containing cross-cutting concerns shared by all modules:

- `IAuditableTenantEntity`, `IAuditableEntity`, `ISoftDeletable`, `IHasId`
- `AuditInfo` value object
- `ITenantProvider`, `IActorProvider`
- `PagedResponse<T>`
- `IUnitOfWork` abstraction
- `IRepository<T>` base interface
- Multi-tenancy infrastructure (global query filters, tenant resolution)
- Soft-delete base infrastructure
- Common domain exceptions (`NotFoundException`, `ValidationException`)
- Audit stamping logic

#### Step 3: Split AppDbContext

Replace the single `AppDbContext` with per-module DbContexts:

- `ProductCatalogDbContext` — Products, Categories, ProductDataLinks, ProductCategoryStats
- `ReviewsDbContext` — ProductReviews
- `IdentityDbContext` — AppUsers, Tenants, TenantInvitations
- `NotificationsDbContext` — FailedEmails
- `FileStorageDbContext` — StoredFiles
- `BackgroundJobsDbContext` — JobExecutions

All contexts share the same PostgreSQL database but enforce module boundaries — a module must not query another module's tables directly.

#### Step 4: Define Module Contracts (Events)

Create `src/Contracts/` as a shared NuGet package containing only:

- Integration events (cross-module communication)
- Shared DTOs for inter-module queries
- No domain logic, no entities

Example events:
```
ProductCreatedEvent { ProductId, TenantId, Name }
ProductDeletedEvent { ProductId, TenantId }
UserRegisteredEvent { UserId, TenantId, Email }
TenantDeactivatedEvent { TenantId }
```

#### Step 5: Replace Direct Cross-Module Calls with Events

Current direct dependencies to refactor:

| Caller | Callee | Current | Target |
|--------|--------|---------|--------|
| Product soft-delete | Reviews cascade | `ProductSoftDeleteCascadeRule` calls ReviewRepository directly | Publish `ProductDeletedEvent` → Reviews module handles cascade |
| Tenant soft-delete | Users, Products cascade | `TenantSoftDeleteCascadeRule` accesses multiple repositories | Publish `TenantDeactivatedEvent` → each module handles own cleanup |
| ProductReview creation | User validation | Queries UserRepository | Reviews module stores read-only user projection, updated via `UserUpdatedEvent` |
| Product creation | Category validation | Queries CategoryRepository | Both in same module (Product Catalog) — no change needed |
| Email handlers | User/Tenant data | Queries user/tenant repos | Notifications module receives all needed data in the event payload |

#### Step 6: Enforce Module Isolation

- Each module exposes only its public API (controllers, events, query interfaces)
- No module references another module's `Domain` or `Infrastructure` project
- Communication exclusively through Wolverine events (in-process for now)
- Add architecture tests (NetArchTest or ArchUnitNET) to enforce boundaries

#### Step 7: Split GraphQL Schema

- Each module defines its own GraphQL types, queries, and mutations
- Use Hot Chocolate Schema Stitching to compose the unified schema
- Prepare for future Hot Chocolate Federation when modules become services

#### Step 8: Split REST Controllers

- Move controllers into their respective module's `Api` project
- Host module still composes all endpoints in `Program.cs`
- Each module registers its own services via `IServiceCollection` extensions

---

### Phase 2 — Strangler Fig Extraction

Extract modules into independent services when scaling demands it. Start with the least coupled modules.

#### Extraction Order (least to most coupled)

1. **Notifications** — no inbound queries, only consumes events
2. **File Storage** — simple CRUD, minimal dependencies
3. **Webhooks** — event-driven by nature
4. **Background Jobs** — independent scheduler
5. **Reviews** — depends on Product (read-only projection)
6. **Identity & Tenancy** — central but well-defined API (Keycloak handles heavy lifting)
7. **Product Catalog** — core domain, extract last

#### Step 1: Deploy API Gateway

- Add YARP or Ocelot as reverse proxy
- Route all traffic through gateway
- Initially, gateway proxies everything to the monolith

#### Step 2: Extract First Service (Notifications)

1. Create standalone ASP.NET project from Notifications module
2. Give it its own PostgreSQL database (or schema)
3. Switch Wolverine transport from in-process to RabbitMQ:
   ```csharp
   // Before (in-process)
   opts.PublishMessage<UserRegisteredEmailEvent>().Locally();

   // After (distributed)
   opts.PublishMessage<UserRegisteredEmailEvent>()
       .ToRabbitQueue("notifications");
   ```
4. Update API Gateway to route `/api/v1/notifications/*` to new service
5. Remove Notifications module from monolith

#### Step 3: Configure Distributed Messaging

- Deploy RabbitMQ (or Azure Service Bus)
- Enable Wolverine outbox pattern for guaranteed delivery:
  ```csharp
  opts.UseRabbitMq(rabbit => { ... })
      .AutoProvision()
      .UseConventionalRouting();
  opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
  ```
- Each service gets its own durable inbox/outbox

#### Step 4: Database-per-Service

- Each extracted service gets its own PostgreSQL database
- Migrate data from shared DB to service-owned DB
- Remove tables from monolith's DB after migration
- MongoDB stays with Product Catalog service

#### Step 5: Repeat for Each Service

Follow the same pattern for each module in extraction order:
1. Stand up independent service from module code
2. Point its Wolverine transport to RabbitMQ
3. Give it its own database
4. Update API Gateway routing
5. Remove module from monolith

#### Step 6: Handle Cross-Service Queries

For queries that span multiple services:

- **API Composition** — Gateway aggregates responses from multiple services
- **CQRS Read Models** — Services maintain denormalized read projections updated via events
- **GraphQL Federation** — Hot Chocolate Federation composes subgraphs from each service

---

## Infrastructure Requirements

### Phase 1 (Modular Monolith)
- No new infrastructure needed
- Same PostgreSQL, MongoDB, Redis/DragonFly, Keycloak

### Phase 2 (Microservices)
- **Message Broker:** RabbitMQ or Azure Service Bus
- **API Gateway:** YARP or Ocelot
- **Container Orchestration:** Docker Compose (dev) → Kubernetes (prod)
- **Service Discovery:** Kubernetes DNS or Consul
- **Distributed Tracing:** Already have OpenTelemetry — works across services
- **Centralized Logging:** Already have Serilog + OTLP — works across services
- **Per-Service Databases:** Multiple PostgreSQL instances (or schemas)

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Data inconsistency across services | Wolverine outbox pattern + idempotent handlers (already have `IIdempotencyStore`) |
| Lost events | Durable messaging with RabbitMQ persistent queues + Wolverine dead letter queue |
| Debugging complexity | OpenTelemetry distributed tracing (already configured) |
| Service discovery failures | Kubernetes DNS + health checks (already have health check infrastructure) |
| Database migration errors | Per-module DbContext in Phase 1 validates data boundaries before physical split |
| GraphQL schema fragmentation | Hot Chocolate Federation maintains unified schema |

---

## Key Wolverine Advantages

The current Wolverine setup makes this transition significantly easier:

1. **Transport agnostic** — switch from in-process to RabbitMQ/Azure SB with config change, not code change
2. **Built-in outbox** — guaranteed message delivery across service boundaries
3. **Durable inbox** — idempotent message processing
4. **Saga support** — orchestrate multi-service workflows
5. **FluentValidation middleware** — works identically in monolith and microservices
6. **Handler discovery** — static handlers work the same regardless of deployment topology
