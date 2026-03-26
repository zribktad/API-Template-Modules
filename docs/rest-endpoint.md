# How to Create a REST Endpoint

This guide walks through the full workflow for adding a new versioned REST endpoint to the API. The example adds an `Orders` resource, following the same patterns used by `Products`, `Categories`, and `ProductReviews`.

---

## Overview

The REST layer follows **Clean Architecture** with **CQRS** via WolverineFx:

```
HTTP Request
  → Controller  (Api/Controllers/V1/)                         ← thin, dispatches via IMessageBus
  → Handler     (Application/Features/<Feature>/)             ← CQRS commands & queries (static HandleAsync)
  → Repository  (Infrastructure/Repositories/)                ← data access (Ardalis.Specification)
  → Database    (PostgreSQL via EF Core)
```

Key boundaries:

- Controllers dispatch Wolverine commands/queries via `IMessageBus` — no business logic.
- Handlers orchestrate business rules, use repositories and `IUnitOfWork` for writes.
- Specifications encapsulate all query logic (filtering, sorting, paging, projection).
- Repositories extend `Ardalis.Specification` — command-side writes + specification-based reads.
- Use `IUnitOfWork.ExecuteInTransactionAsync(...)` for transactional writes.

---

## Step 1 – Define the Domain Entity

Create the entity in `src/APITemplate.Domain/Domain/Entities/`. All entities implement `IAuditableTenantEntity` (multi-tenancy + auditing + soft delete):

```csharp
// Domain/Entities/Order.cs
namespace APITemplate.Domain.Entities;

public sealed class Order : IAuditableTenantEntity
{
    public Guid Id { get; set; }

    public required string CustomerName
    {
        get => field;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Customer name cannot be empty.", nameof(CustomerName))
            : value.Trim();
    }

    public decimal TotalAmount { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];

    // IAuditableTenantEntity
    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
```

> **Note:** Put domain validation in property setters (e.g., trimming, null checks). `AuditInfo` fields are stamped automatically by `AppDbContext.SaveChangesAsync`.

---

## Step 2 – Create the DTOs

DTOs live in `src/APITemplate.Application/Features/<Feature>/DTOs/`.

**Filter DTO** (query parameters — extends `PaginationFilter`):

```csharp
// Application/Features/Order/DTOs/OrderFilter.cs
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.DTOs;

namespace APITemplate.Application.Features.Order.DTOs;

public sealed record OrderFilter(
    string? Query = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
```

**Response DTO** (what the API returns):

```csharp
// Application/Features/Order/DTOs/OrderResponse.cs
namespace APITemplate.Application.Features.Order.DTOs;

public sealed record OrderResponse(
    Guid Id,
    string CustomerName,
    decimal TotalAmount,
    DateTime CreatedAtUtc);
```

**Request DTOs** (what the client sends):

```csharp
// Application/Features/Order/DTOs/CreateOrderRequest.cs
namespace APITemplate.Application.Features.Order.DTOs;

public sealed record CreateOrderRequest(
    string CustomerName,
    decimal TotalAmount);
```

```csharp
// Application/Features/Order/DTOs/UpdateOrderRequest.cs
namespace APITemplate.Application.Features.Order.DTOs;

public sealed record UpdateOrderRequest(
    string CustomerName,
    decimal TotalAmount);
```

---

## Step 3 – Add the FluentValidation Validators

Validators live in `src/APITemplate.Application/Features/<Feature>/Validation/`. They are auto-discovered and invoked by Wolverine's FluentValidation middleware (`UseFluentValidation()`) before handlers run.

```csharp
// Application/Features/Order/Validation/OrderFilterValidator.cs
using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Order.Validation;

public sealed class OrderFilterValidator : AbstractValidator<OrderFilter>
{
    public OrderFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new SortableFilterValidator<OrderFilter>(OrderSortFields.Map.AllowedNames));
    }
}
```

For request validators, use `AbstractValidator<T>` or inherit from a shared base when Create/Update share rules:

```csharp
// Application/Features/Order/Validation/CreateOrderRequestValidator.cs
using FluentValidation;

namespace APITemplate.Application.Features.Order.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("CustomerName is required.")
            .MaximumLength(200).WithMessage("CustomerName must not exceed 200 characters.");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("TotalAmount must be greater than zero.");
    }
}
```

Validation failures throw `Domain.Exceptions.ValidationException`, which is mapped to HTTP 400 by `ApiExceptionHandler`.

---

## Step 4 – Define the Mapping Extension

Mappings use **Expression projections** for EF Core query efficiency. Place them in `src/APITemplate.Application/Features/<Feature>/Mappings/`:

```csharp
// Application/Features/Order/Mappings/OrderMappings.cs
using System.Linq.Expressions;
using OrderEntity = APITemplate.Domain.Entities.Order;

namespace APITemplate.Application.Features.Order.Mappings;

public static class OrderMappings
{
    public static readonly Expression<Func<OrderEntity, OrderResponse>> Projection =
        order => new OrderResponse(
            order.Id,
            order.CustomerName,
            order.TotalAmount,
            order.Audit.CreatedAtUtc);

    private static readonly Func<OrderEntity, OrderResponse> CompiledProjection = Projection.Compile();

    public static OrderResponse ToResponse(this OrderEntity order) =>
        CompiledProjection(order);
}
```

> `Projection` is used by specifications for server-side SELECT. `ToResponse()` is used in handlers after entity creation/update.

---

## Step 5 – Define the Specifications

Specifications encapsulate query logic using `Ardalis.Specification`. Place them in `src/APITemplate.Application/Features/<Feature>/Specifications/`.

**List specification** (filtered, sorted, projected — no Skip/Take):

```csharp
// Application/Features/Order/Specifications/OrderSpecification.cs
using APITemplate.Application.Features.Order.Mappings;
using Ardalis.Specification;
using OrderEntity = APITemplate.Domain.Entities.Order;

namespace APITemplate.Application.Features.Order.Specifications;

public sealed class OrderSpecification : Specification<OrderEntity, OrderResponse>
{
    public OrderSpecification(OrderFilter filter)
    {
        Query.ApplyFilter(filter);
        Query.AsNoTracking();
        OrderSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);
        Query.Select(OrderMappings.Projection);
        // No Skip/Take here — pagination is handled by repository.GetPagedAsync()
    }
}
```

> **Important:** Do NOT add `Skip`/`Take` to the specification. `RepositoryBase.GetPagedAsync` handles pagination and total count in a single optimized SQL query.

**By-ID specification**:

```csharp
// Application/Features/Order/Specifications/OrderByIdSpecification.cs
using APITemplate.Application.Features.Order.Mappings;
using Ardalis.Specification;
using OrderEntity = APITemplate.Domain.Entities.Order;

namespace APITemplate.Application.Features.Order.Specifications;

public sealed class OrderByIdSpecification : Specification<OrderEntity, OrderResponse>
{
    public OrderByIdSpecification(Guid id)
    {
        Query.Where(order => order.Id == id)
            .AsNoTracking()
            .Select(OrderMappings.Projection);
    }
}
```

**Filter criteria** (reusable filter logic):

```csharp
// Application/Features/Order/Specifications/OrderFilterCriteria.cs
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using OrderEntity = APITemplate.Domain.Entities.Order;

namespace APITemplate.Application.Features.Order.Specifications;

internal static class OrderFilterCriteria
{
    private const string SearchConfiguration = "english";

    internal static void ApplyFilter(this ISpecificationBuilder<OrderEntity> query, OrderFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Query))
            return;

        query.Where(order =>
            EF.Functions
                .ToTsVector(SearchConfiguration, order.CustomerName)
                .Matches(EF.Functions.WebSearchToTsQuery(SearchConfiguration, filter.Query)));
    }
}
```

---

## Step 6 – Define the Sort Fields

Sort field maps provide type-safe, configurable sorting. Place in `src/APITemplate.Application/Features/<Feature>/`:

```csharp
// Application/Features/Order/OrderSortFields.cs
using APITemplate.Application.Common.Sorting;
using OrderEntity = APITemplate.Domain.Entities.Order;

namespace APITemplate.Application.Features.Order;

public static class OrderSortFields
{
    public static readonly SortField CustomerName = new("customerName");
    public static readonly SortField TotalAmount = new("totalAmount");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<OrderEntity> Map = new SortFieldMap<OrderEntity>()
        .Add(CustomerName, o => o.CustomerName)
        .Add(TotalAmount, o => (object)o.TotalAmount)
        .Add(CreatedAt, o => o.Audit.CreatedAtUtc)
        .Default(o => o.Audit.CreatedAtUtc);
}
```

---

## Step 7 – Define the Wolverine Handlers

Commands, queries, and handlers are placed as separate files per operation in `src/APITemplate.Application/Features/<Feature>/`. Wolverine discovers handlers by convention — each handler is a sealed class with a static `HandleAsync` method. Dependencies are injected as method parameters:

**Query example:**

```csharp
// Application/Features/Order/Queries/GetOrdersQuery.cs
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Features.Order.DTOs;
using APITemplate.Application.Features.Order.Specifications;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Order;

public sealed record GetOrdersQuery(OrderFilter Filter);

public sealed class GetOrdersQueryHandler
{
    public static async Task<PagedResponse<OrderResponse>> HandleAsync(
        GetOrdersQuery query,
        IOrderRepository repository,
        CancellationToken ct)
    {
        return await repository.GetPagedAsync(
            new OrderSpecification(query.Filter), query.Filter.PageNumber, query.Filter.PageSize, ct);
    }
}
```

**Command example:**

```csharp
// Application/Features/Order/Commands/CreateOrderCommand.cs
using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Order.DTOs;
using APITemplate.Application.Features.Order.Mappings;
using APITemplate.Domain.Interfaces;
using Wolverine;
using OrderEntity = APITemplate.Domain.Entities.Order;

namespace APITemplate.Application.Features.Order;

public sealed record CreateOrderCommand(CreateOrderRequest Request);

public sealed class CreateOrderCommandHandler
{
    public static async Task<OrderResponse> HandleAsync(
        CreateOrderCommand command,
        IOrderRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        CancellationToken ct)
    {
        var order = await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var entity = new OrderEntity
            {
                Id = Guid.NewGuid(),
                CustomerName = command.Request.CustomerName,
                TotalAmount = command.Request.TotalAmount
            };

            await repository.AddAsync(entity, ct);
            return entity;
        }, ct);

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Orders));
        return order.ToResponse();
    }
}
```

> **Note:** Add cache tag constant to `CacheTags` and error code to `ErrorCatalog.Orders.NotFound` in `Application/Common/Errors/ErrorCatalog.cs`.

---

## Step 8 – Create the Repository

**Interface** in `src/APITemplate.Domain/Domain/Interfaces/`:

```csharp
// Domain/Interfaces/IOrderRepository.cs
using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order> { }
```

`IRepository<T>` extends `Ardalis.Specification.IRepositoryBase<T>` and adds `DeleteAsync(Guid id, ...)`.

**Implementation** in `src/APITemplate.Infrastructure/Repositories/`:

```csharp
// Infrastructure/Repositories/OrderRepository.cs
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

public sealed class OrderRepository : RepositoryBase<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext dbContext) : base(dbContext) { }
}
```

`RepositoryBase<T>` overrides `AddAsync`/`UpdateAsync` to **not** call `SaveChangesAsync` — that is the `IUnitOfWork` responsibility.

---

## Step 9 – Add the EF Core Configuration

Add `DbSet<Order>` to `AppDbContext` and create the entity configuration in `src/APITemplate.Infrastructure/Persistence/Configurations/`:

```csharp
// Infrastructure/Persistence/Configurations/OrderConfiguration.cs
using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(o => o.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.TenantId, o.CustomerName });
    }
}
```

> `ConfigureTenantAuditable()` is an extension method that configures `AuditInfo`, `TenantId`, `IsDeleted`, and soft delete fields.

---

## Step 10 – Add the Controller

Controllers live in `src/APITemplate.Api/Api/Controllers/V1/`. They dispatch via Wolverine `IMessageBus`:

```csharp
// Api/Controllers/V1/OrdersController.cs
using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Application.Common.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
public sealed class OrdersController(IMessageBus bus) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.Orders.Read)]
    [OutputCache(PolicyName = CacheTags.Orders)]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> GetAll(
        [FromQuery] OrderFilter filter, CancellationToken ct)
    {
        var orders = await bus.InvokeAsync<PagedResponse<OrderResponse>>(new GetOrdersQuery(filter), ct);
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Orders.Read)]
    [OutputCache(PolicyName = CacheTags.Orders)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken ct)
    {
        var order = await bus.InvokeAsync<OrderResponse?>(new GetOrderByIdQuery(id), ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [RequirePermission(Permission.Orders.Create)]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request, CancellationToken ct)
    {
        var order = await bus.InvokeAsync<OrderResponse>(new CreateOrderCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id, version = "1.0" }, order);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.Orders.Update)]
    public async Task<IActionResult> Update(Guid id, UpdateOrderRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateOrderCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Orders.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteOrderCommand(id), ct);
        return NoContent();
    }
}
```

---

## Step 11 – Register in Dependency Injection

**Repository** — add to `AddPersistence()` in `src/APITemplate.Api/Extensions/PersistenceServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
```

**Permissions** — add to `Application/Common/Security/Permission.cs`:

```csharp
public static class Orders
{
    public const string Read = "Orders.Read";
    public const string Create = "Orders.Create";
    public const string Update = "Orders.Update";
    public const string Delete = "Orders.Delete";
}
```

**Error codes** — add to `Application/Common/Errors/ErrorCatalog.cs`:

```csharp
public static class Orders
{
    public const string NotFound = "ORD-0404";
}
```

**Cache tag** — add to `Application/Common/Events/CacheTags.cs`:

```csharp
public const string Orders = "Orders";
```

> Wolverine handlers and FluentValidation validators are auto-discovered from the assembly — no explicit registration needed. Wolverine is configured in `Program.cs` via `UseWolverine()` with `UseFluentValidation()` middleware.

---

## Step 12 – Create the EF Core Migration

After adding the `DbSet<Order>` to `AppDbContext` and the entity configuration:

```bash
dotnet ef migrations add AddOrder --project src/APITemplate.Infrastructure --startup-project src/APITemplate.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/APITemplate.Infrastructure --startup-project src/APITemplate.Api
```

See [ef-migration.md](ef-migration.md) for the full migration workflow.

---

## HTTP Endpoints Summary

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| `GET` | `/api/v1/Orders` | `Orders.Read` | Paginated, filtered, sorted list |
| `GET` | `/api/v1/Orders/{id}` | `Orders.Read` | Single item |
| `POST` | `/api/v1/Orders` | `Orders.Create` | Create |
| `PUT` | `/api/v1/Orders/{id}` | `Orders.Update` | Update |
| `DELETE` | `/api/v1/Orders/{id}` | `Orders.Delete` | Delete |

To obtain a Bearer token, see [AUTHENTICATION.md](AUTHENTICATION.md).

---

## Feature Directory Structure

```
Application/Features/Order/
├── DTOs/
│   ├── OrderFilter.cs
│   ├── OrderResponse.cs
│   ├── CreateOrderRequest.cs
│   └── UpdateOrderRequest.cs
├── Commands/
│   ├── CreateOrderCommand.cs         ← command record + handler class
│   ├── UpdateOrderCommand.cs
│   └── DeleteOrderCommand.cs
├── Queries/
│   ├── GetOrdersQuery.cs             ← query record + handler class
│   └── GetOrderByIdQuery.cs
├── Mappings/
│   └── OrderMappings.cs              ← Expression projections
├── Specifications/
│   ├── OrderSpecification.cs          ← filtered list query (no Skip/Take)
│   ├── OrderByIdSpecification.cs      ← single entity lookup
│   └── OrderFilterCriteria.cs         ← shared filter logic
├── Validation/
│   ├── OrderFilterValidator.cs
│   └── CreateOrderRequestValidator.cs
└── OrderSortFields.cs                 ← sort field mappings
```

---

## Checklist

- [ ] Domain entity implementing `IAuditableTenantEntity` in `Domain/Entities/`
- [ ] Filter + Response + Request DTOs in `Application/Features/<Feature>/DTOs/`
- [ ] FluentValidation validators in `Application/Features/<Feature>/Validation/`
- [ ] Expression projection mappings in `Application/Features/<Feature>/Mappings/`
- [ ] Specifications (list, byId, filter criteria) in `Application/Features/<Feature>/Specifications/`
- [ ] Sort field map in `Application/Features/<Feature>/`
- [ ] Wolverine commands, queries & handlers in `Application/Features/<Feature>/`
- [ ] Repository interface in `Domain/Interfaces/`
- [ ] Repository implementation in `Infrastructure/Repositories/`
- [ ] EF Core entity configuration in `Infrastructure/Persistence/Configurations/`
- [ ] Controller in `Api/Controllers/V1/`
- [ ] Repository DI registration in `PersistenceServiceCollectionExtensions.cs`
- [ ] Permissions in `Permission.cs`
- [ ] Error codes in `ErrorCatalog.cs`
- [ ] Cache tag constant in `CacheTags.cs`
- [ ] EF Core migration (see [ef-migration.md](ef-migration.md))

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Api/Controllers/V1/` | HTTP endpoint definitions (thin, IMessageBus dispatch) |
| `Application/Features/<Feature>/DTOs/` | Filter, request & response contracts |
| `Application/Features/<Feature>/` | Wolverine commands, queries & handlers (static HandleAsync) |
| `Application/Features/<Feature>/Specifications/` | Ardalis.Specification query logic |
| `Application/Features/<Feature>/Mappings/` | Expression projections (Entity → DTO) |
| `Application/Features/<Feature>/Validation/` | FluentValidation validators |
| `Application/Features/<Feature>/<Feature>SortFields.cs` | Sort field mappings |
| `Application/Common/DTOs/` | `PagedResponse<T>`, `PaginationFilter` |
| `Application/Common/Security/Permission.cs` | Permission constants |
| `Application/Common/Errors/ErrorCatalog.cs` | Error code constants |
| `Application/Common/Events/CacheTags.cs` | Cache tag constants for invalidation |
| `Domain/Entities/` | Domain models (`IAuditableTenantEntity`) |
| `Domain/Interfaces/` | Repository contracts (`IRepository<T>`) |
| `Infrastructure/Repositories/` | EF Core repository implementations |
| `Infrastructure/Persistence/Configurations/` | EF Core entity configurations |
| `Api/Extensions/PersistenceServiceCollectionExtensions.cs` | Repository DI registration |
