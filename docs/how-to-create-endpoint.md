# How to Create a New API Endpoint

Step-by-step guide for adding a new REST API endpoint to this project. Uses **Product** as the reference implementation.

---

## Architecture Overview

```
HTTP Request
  → Controller (Api)
    → Wolverine IMessageBus (dispatch)
      → Command/Query Handler (Application)
        → Repository (Infrastructure)
          → Database (PostgreSQL)
```

Layers follow **Clean Architecture** — dependencies point inward:

```
Domain  ←  Application  ←  Infrastructure  ←  Api
```

**WolverineFx** runs in mediator-only mode — no service layer interfaces. Handlers ARE the service layer. The controller dispatches messages via `IMessageBus`, and Wolverine routes them to the matching handler by naming convention.

---

## File Structure per Feature

```
Application/Features/{Feature}/
├── Commands/
│   ├── Create{Feature}sCommand.cs    # Command message + handler (write)
│   ├── Update{Feature}sCommand.cs    # Command message + handler (write)
│   └── Delete{Feature}sCommand.cs    # Command message + handler (write)
├── Queries/
│   ├── Get{Feature}sQuery.cs         # Query message + handler (list)
│   └── Get{Feature}ByIdQuery.cs      # Query message + handler (single)
├── DTOs/
│   ├── Create{Feature}Request.cs     # Input DTO (create)
│   ├── Update{Feature}Request.cs     # Input DTO (update)
│   ├── {Feature}Response.cs          # Output DTO
│   └── {Feature}Filter.cs            # Query/filter parameters
├── Specifications/
│   ├── {Feature}Specification.cs     # Filtered/sorted/projected query (no pagination)
│   └── {Feature}FilterCriteria.cs    # Reusable filter logic
├── Validation/
│   ├── Create{Feature}RequestValidator.cs
│   ├── Update{Feature}RequestValidator.cs
│   └── {Feature}FilterValidator.cs
├── Mappings/
│   └── {Feature}Mappings.cs          # Entity → Response mapping
└── {Feature}SortFields.cs            # Sort field definitions
```

Additional files outside the feature folder:

```
Domain/Entities/{Feature}.cs                              # Domain entity
Domain/Interfaces/I{Feature}Repository.cs                 # Repository contract
Infrastructure/Repositories/{Feature}Repository.cs        # Repository implementation
Infrastructure/Persistence/Configurations/{Feature}Configuration.cs  # EF Core config
Api/Controllers/V1/{Feature}sController.cs                # REST controller
Application/Common/Errors/ErrorCatalog.cs                 # Error codes (add section)
Extensions/ServiceCollectionExtensions.cs                 # DI registration (repository only)
```

---

## Step-by-Step Guide

### 1. Domain Entity

Create `Domain/Entities/{Feature}.cs`. Implement `IAuditableTenantEntity` for full multi-tenancy, auditing, soft-delete, and concurrency support.

```csharp
namespace APITemplate.Domain.Entities;

public sealed class Product : IAuditableTenantEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }

    // Relationships
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    // IAuditableTenantEntity members (always include these)
    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
```

> `IAuditableTenantEntity` = `ITenantEntity` + `IAuditableEntity` + `ISoftDeletable`. The `AppDbContext` auto-handles tenancy, auditing, and soft-delete for any entity implementing this interface. Optimistic concurrency is handled automatically via the PostgreSQL `xmin` system column.

---

### 2. EF Core Configuration

Create `Infrastructure/Persistence/Configurations/{Feature}Configuration.cs`:

```csharp
using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ConfigureTenantAuditable(); // Sets up audit, tenant, soft-delete, row version

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);

        // Relationships
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(p => new { p.TenantId, p.Name });
    }
}
```

> Always call `builder.ConfigureTenantAuditable()` — it sets up audit fields, soft-delete, row version, tenant indexes, and a check constraint for soft-delete consistency.

Add a `DbSet` to `AppDbContext`:

```csharp
public DbSet<Product> Products => Set<Product>();
```

---

### 3. Repository

**Interface** — `Domain/Interfaces/I{Feature}Repository.cs`:

```csharp
using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    // Add feature-specific query methods here if needed
}
```

> `IRepository<T>` inherits from Ardalis `IRepositoryBase<T>` and adds `DeleteAsync(Guid id)`.

**Implementation** — `Infrastructure/Repositories/{Feature}Repository.cs`:

```csharp
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public ProductRepository(AppDbContext dbContext) : base(dbContext) { }
}
```

> `RepositoryBase<T>` overrides `AddAsync`/`UpdateAsync` to NOT call `SaveChangesAsync` — that's the `IUnitOfWork`'s job.

---

### 4. DTOs

**Request DTOs** — `Application/Features/{Feature}/DTOs/`:

```csharp
using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Product.DTOs;

public sealed record CreateProductRequest(
    [NotEmpty(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
    string Name,
    string? Description,
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    decimal Price,
    Guid? CategoryId = null) : IProductRequest;
```

> Use **Data Annotations** for simple per-field validation. Use **FluentValidation** for cross-field rules.

**Response DTO**:

```csharp
namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAtUtc);
```

**Filter DTO** (for GET all / list endpoints):

```csharp
using APITemplate.Application.Common.Contracts;

namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
```

> Implement `ISortableFilter` for sorting, `IDateRangeFilter` for date range filtering. Extend `PaginationFilter` for page number/size validation.

---

### 5. Mappings

`Application/Features/{Feature}/Mappings/{Feature}Mappings.cs`:

```csharp
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Mappings;

public static class ProductMappings
{
    public static ProductResponse ToResponse(this ProductEntity product) =>
        new(product.Id, product.Name, product.Description, product.Price, product.Audit.CreatedAtUtc);
}
```

> Static extension methods — no mapping library needed. Simple and explicit.

---

### 6. Sort Fields

`Application/Features/{Feature}/{Feature}SortFields.cs`:

```csharp
using APITemplate.Application.Common.Sorting;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

public static class ProductSortFields
{
    public static readonly SortField Name = new("name");
    public static readonly SortField Price = new("price");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<ProductEntity> Map = new SortFieldMap<ProductEntity>()
        .Add(Name, p => p.Name)
        .Add(Price, p => (object)p.Price)
        .Default(p => p.Audit.CreatedAtUtc);
}
```

> Single source of truth for sort field names and expressions. Used by both validators and specifications. To add a new sort field, just add one `.Add(...)` line.

---

### 7. Specifications

**Filter specification** — `Application/Features/{Feature}/Specifications/{Feature}Specification.cs`:

```csharp
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

public sealed class ProductSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        // 1. Filter
        Query.ApplyFilter(filter);

        // 2. Sort
        ProductSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        // 3. Project to DTO
        Query.Select(p => new ProductResponse(
            p.Id, p.Name, p.Description, p.Price, p.Audit.CreatedAtUtc));

        // No Skip/Take here — pagination is handled by repository.GetPagedAsync()
    }
}
```

> **Important:** Do NOT add `Skip`/`Take` to the specification. `RepositoryBase.GetPagedAsync` handles pagination and total count in a single optimized SQL query.

**Filter criteria** (reusable filter logic):

```csharp
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

internal static class ProductFilterCriteria
{
    internal static void ApplyFilter(this ISpecificationBuilder<ProductEntity> query, ProductFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
            query.Where(p => p.Name.Contains(filter.Name));

        if (!string.IsNullOrWhiteSpace(filter.Description))
            query.Where(p => p.Description != null && p.Description.Contains(filter.Description));

        if (filter.MinPrice.HasValue)
            query.Where(p => p.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc <= filter.CreatedTo.Value);
    }
}
```

---

### 8. Validators

Use FluentValidation for cross-field rules. Data Annotations handle simple per-field validation.

**Shared request validator base** (for rules shared between Create and Update):

```csharp
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;

public abstract class ProductRequestValidatorBase<T> : AbstractValidator<T>
    where T : IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        // Cross-field rule: cannot be expressed via Data Annotations
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required for products priced above 1000.")
            .When(x => x.Price > 1000);
    }
}
```

**Create/Update validators** (inherit shared rules):

```csharp
namespace APITemplate.Application.Features.Product.Validation;

public sealed class CreateProductRequestValidator : ProductRequestValidatorBase<CreateProductRequest>;
public sealed class UpdateProductRequestValidator : ProductRequestValidatorBase<UpdateProductRequest>;
```

**Filter validator**:

```csharp
using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;

public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new DateRangeFilterValidator<ProductFilter>());
        Include(new SortableFilterValidator<ProductFilter>(ProductSortFields.Map.AllowedNames));

        // Feature-specific filter rules
        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("MinPrice must be >= 0.")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).WithMessage("MaxPrice must be >= 0.")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .WithMessage("MaxPrice must be >= MinPrice.")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);
    }
}
```

> Validators are auto-discovered and registered via `AddValidatorsFromAssemblyContaining<>()` in DI. No manual registration needed.

---

### 9. Command & Query Handlers

Handlers replace the old service layer. There are no service interfaces — handlers ARE the service layer. Each handler is a `sealed class` with a `static HandleAsync` method. Wolverine auto-discovers handlers by naming convention (`{Message}Handler` class with a `HandleAsync` method).

Dependencies are injected as method parameters (method injection), not via constructor.

#### Query Handler (read operations)

`Application/Features/{Feature}/Queries/Get{Feature}sQuery.cs`:

```csharp
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product;

/// <summary>Retrieves a filtered, sorted, and paged list of products.</summary>
public sealed record GetProductsQuery(ProductFilter Filter);

public sealed class GetProductsQueryHandler
{
    public static async Task<ProductsResponse> HandleAsync(
        GetProductsQuery request,
        IProductRepository repository,
        CancellationToken ct
    )
    {
        var page = await repository.GetPagedAsync(request.Filter, ct);
        return new ProductsResponse(page);
    }
}
```

`Application/Features/{Feature}/Queries/Get{Feature}ByIdQuery.cs`:

```csharp
using APITemplate.Application.Features.Product.Mappings;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product;

/// <summary>Retrieves a single product by its identifier.</summary>
public sealed record GetProductByIdQuery(Guid Id);

public sealed class GetProductByIdQueryHandler
{
    public static async Task<ProductResponse?> HandleAsync(
        GetProductByIdQuery request,
        IProductRepository repository,
        CancellationToken ct
    )
    {
        var item = await repository.GetByIdAsync(request.Id, ct);
        return item?.ToResponse();
    }
}
```

> **Pattern**: The message (record) and its handler live in the same file. The first parameter of `HandleAsync` is always the message type. All other parameters are method-injected by Wolverine from DI.

#### Command Handler (write operations)

`Application/Features/{Feature}/Commands/Create{Feature}sCommand.cs`:

```csharp
using APITemplate.Application.Common.Batch;
using APITemplate.Domain.Interfaces;
using FluentValidation;
using Wolverine;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

/// <summary>Creates one or more products in a single batch operation.</summary>
public sealed record CreateProductsCommand(CreateProductsRequest Request);

public sealed class CreateProductsCommandHandler
{
    public static async Task<BatchResponse> HandleAsync(
        CreateProductsCommand command,
        IProductRepository repository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IValidator<CreateProductRequest> itemValidator,
        CancellationToken ct
    )
    {
        var items = command.Request.Items;
        var context = new BatchFailureContext<CreateProductRequest>(items);

        // Step 1: Validate each item
        await context.ApplyRulesAsync(
            ct,
            new FluentValidationBatchRule<CreateProductRequest>(itemValidator)
        );

        if (context.HasFailures)
            return context.ToFailureResponse();

        // Step 2: Build entities and persist
        var entities = items
            .Select(item => new ProductEntity
            {
                Id = Guid.NewGuid(),
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                CategoryId = item.CategoryId,
            })
            .ToList();

        await unitOfWork.ExecuteInTransactionAsync(
            async () => await repository.AddRangeAsync(entities, ct),
            ct
        );

        return new BatchResponse([], items.Count, 0);
    }
}
```

> **Key patterns**:
> - Repository tracks changes, `IUnitOfWork` persists them. Never call `SaveChangesAsync` in repositories.
> - `IValidator<T>` is method-injected for per-item validation inside batch commands.
> - `IMessageBus` can be injected to publish domain events (e.g., cache invalidation).

---

### 10. Error Codes

Add a section to `Application/Common/Errors/ErrorCatalog.cs`:

```csharp
public static class Products
{
    public const string NotFound = "PRD-0404";
}
```

> Convention: `{PREFIX}-{HTTP_STATUS}`. Used in `NotFoundException` for structured error responses.

---

### 11. Controller

`Api/Controllers/V1/{Feature}sController.cs`:

Controllers use `IMessageBus` via primary constructor to dispatch commands and queries to Wolverine handlers. They inherit from `ApiControllerBase` which provides the versioned route template and helper methods.

```csharp
using APITemplate.Api.Controllers;
using APITemplate.Application.Common.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
public sealed class ProductsController(IMessageBus bus) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.Products.Read)]
    public async Task<ActionResult<ProductsResponse>> GetAll(
        [FromQuery] ProductFilter filter,
        CancellationToken ct
    )
    {
        var products = await bus.InvokeAsync<ProductsResponse>(new GetProductsQuery(filter), ct);
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Products.Read)]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken ct
    )
    {
        var product = await bus.InvokeAsync<ProductResponse?>(new GetProductByIdQuery(id), ct);
        return OkOrNotFound(product);
    }

    [HttpPost]
    [RequirePermission(Permission.Products.Create)]
    public async Task<ActionResult<BatchResponse>> Create(
        CreateProductsRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<BatchResponse>(new CreateProductsCommand(request), ct);
        return OkOrUnprocessable(result);
    }

    [HttpPut]
    [RequirePermission(Permission.Products.Update)]
    public async Task<ActionResult<BatchResponse>> Update(
        UpdateProductsRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<BatchResponse>(new UpdateProductsCommand(request), ct);
        return OkOrUnprocessable(result);
    }

    [HttpDelete]
    [RequirePermission(Permission.Products.Delete)]
    public async Task<ActionResult<BatchResponse>> Delete(
        BatchDeleteRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<BatchResponse>(new DeleteProductsCommand(request), ct);
        return OkOrUnprocessable(result);
    }
}
```

> **Key differences from the old pattern**:
> - No service interface injection — only `IMessageBus bus` via primary constructor.
> - `bus.InvokeAsync<TResponse>(message, ct)` dispatches to the matching handler and returns the result.
> - `ApiControllerBase` provides the `[ApiController]` attribute, versioned route template, and helper methods (`OkOrNotFound`, `OkOrUnprocessable`, `CreatedAtGetById`).
> - Authorization uses `[RequirePermission(...)]` instead of `[Authorize]`.
> - Validation of controller DTOs is handled automatically by `FluentValidationActionFilter`.

---

### 12. Register in DI

In `Extensions/ServiceCollectionExtensions.cs`:

**`AddPersistence` method** — add repository:
```csharp
services.AddScoped<IProductRepository, ProductRepository>();
```

That's it. **No handler or service registration needed** — Wolverine auto-discovers all handlers by naming convention (`{Message}Handler` with `HandleAsync`). Validators are auto-registered via `AddValidatorsFromAssemblyContaining<>()`.

> If your feature adds a new repository, that is the only DI registration you need to add. Wolverine and FluentValidation handle everything else.

---

### 13. Create EF Migration

```bash
dotnet ef migrations add Add{Feature} --project src/APITemplate
```

---

## Checklist

- [ ] Domain entity implementing `IAuditableTenantEntity`
- [ ] EF Core configuration with `ConfigureTenantAuditable()`
- [ ] `DbSet` in `AppDbContext`
- [ ] Repository interface (`IRepository<T>`) and implementation (`RepositoryBase<T>`)
- [ ] DTOs: Create request, Update request, Response, Filter
- [ ] Mappings: Entity → Response extension method
- [ ] Sort fields: `SortFieldMap<T>` definition
- [ ] Specifications: main (filtered + sorted + projected — no Skip/Take)
- [ ] Filter criteria: reusable filter logic
- [ ] Validators: request validators, filter validator
- [ ] Query handlers: `sealed class` + `static HandleAsync`, message record in same file
- [ ] Command handlers: `sealed class` + `static HandleAsync`, message record in same file
- [ ] Error codes in `ErrorCatalog`
- [ ] Controller inheriting `ApiControllerBase`, using `IMessageBus` primary constructor
- [ ] Repository DI registration in `ServiceCollectionExtensions` (handlers auto-discovered)
- [ ] EF migration

---

## Cross-Cutting Concerns (handled automatically)

| Concern | How | Where |
|---------|-----|-------|
| **Multi-tenancy** | Global query filter on `TenantId` | `AppDbContext` |
| **Soft delete** | `Remove()` → sets `IsDeleted = true` | `AppDbContext.SaveChangesAsync` |
| **Audit trail** | Auto-stamps `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy` | `AppDbContext.SaveChangesAsync` |
| **Concurrency** | PostgreSQL `xmin` system column as concurrency token → HTTP 409 on conflict | `ApiExceptionHandler` |
| **Command validation** | FluentValidation via Wolverine's `UseFluentValidation()` middleware | Wolverine pipeline |
| **Controller DTO validation** | Data Annotations + FluentValidation via action filter | `FluentValidationActionFilter` |
| **Error handling** | `AppException` hierarchy → RFC 7807 ProblemDetails | `ApiExceptionHandler` |
| **Handler discovery** | Wolverine auto-discovers `{Message}Handler` classes | `UseWolverine()` in host setup |
| **JWT auth** | `[RequirePermission]` + tenant claim validation | Authorization middleware |

---

## Request Flow

```
HTTP Request
  → Exception Handler Middleware
    → Request Context Middleware (extracts tenant/actor from JWT)
      → Authentication Middleware (validates JWT)
        → Authorization Middleware (checks [RequirePermission])
          → FluentValidationActionFilter (validates controller DTOs)
            → Controller action
              → IMessageBus.InvokeAsync (Wolverine dispatch)
                → Wolverine middleware (FluentValidation for commands)
                  → Handler.HandleAsync (business logic)
                    → Repository (data access via Specification)
                      → AppDbContext (auditing, tenancy, soft-delete)
                        → PostgreSQL
```
