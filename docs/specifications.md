# How to Use the Specification Pattern (Ardalis.Specification)

This guide explains how to write reusable, composable query specifications using the **Ardalis.Specification** library. Specifications encapsulate EF Core `Where`, `OrderBy`, `Include`, `Take`, and `Skip` logic so it stays out of repositories and services.

---

## Overview

The specification pattern separates query logic from the data-access layer:

```
Service
  → new ProductSpecification(filter)        ← encapsulates all query logic
  → repository.ListAsync(specification)     ← RepositoryBase executes it
  → EF Core generates optimised SQL
```

The `RepositoryBase<T>` in this project inherits from `Ardalis.Specification.EntityFrameworkCore.RepositoryBase<T>`, which provides `ListAsync(spec)`, `CountAsync(spec)`, and `FirstOrDefaultAsync(spec)` out of the box.

---

## Step 1 – Understand the Existing Examples

### Projection Specification

A `Specification<TEntity, TResult>` selects a projection DTO directly in SQL. Do **not** include `Skip`/`Take` — pagination is handled by `RepositoryBase.GetPagedAsync`:

```csharp
// Application/Specifications/ProductSpecification.cs
public sealed class ProductSpecification : Specification<Product, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        Query.ApplyFilter(filter);

        Query.OrderByDescending(p => p.CreatedAt)
             .Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAt));

        // No Skip/Take here — pagination is handled by repository.GetPagedAsync()
    }
}
```

> **Important:** `GetPagedAsync(spec, pageNumber, pageSize, ct)` applies pagination and retrieves the total count in a single SQL query, eliminating the need for a separate count specification.

### Shared Filter Criteria

Extract reusable `Where` clauses into an extension method on `ISpecificationBuilder<T>`:

```csharp
// Application/Specifications/ProductFilterCriteria.cs
internal static class ProductFilterCriteria
{
    internal static void ApplyFilter(this ISpecificationBuilder<Product> query, ProductFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
            query.Where(p => p.Name.Contains(filter.Name));

        if (filter.MinPrice.HasValue)
            query.Where(p => p.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(p => p.CreatedAt <= filter.CreatedTo.Value);
    }
}
```

---

## Step 2 – Create a New Filter Criteria Class

Define filter criteria for your entity. By convention, name the file `<Entity>FilterCriteria.cs`.

**`src/APITemplate/Application/Specifications/OrderFilterCriteria.cs`**

```csharp
using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

internal static class OrderFilterCriteria
{
    internal static void ApplyFilter(this ISpecificationBuilder<Order> query, OrderFilter filter)
    {
        if (filter.CustomerId.HasValue)
            query.Where(o => o.CustomerId == filter.CustomerId.Value);

        if (filter.MinAmount.HasValue)
            query.Where(o => o.TotalAmount >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query.Where(o => o.TotalAmount <= filter.MaxAmount.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(o => o.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(o => o.CreatedAt <= filter.CreatedTo.Value);
    }
}
```

---

## Step 3 – Create the List Specification

Combines filtering, ordering, and projection in one place. Do **not** add `Skip`/`Take` — pagination is handled by `RepositoryBase.GetPagedAsync`.

**`src/APITemplate/Application/Specifications/OrderSpecification.cs`**

```csharp
using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class OrderSpecification : Specification<Order, OrderResponse>
{
    public OrderSpecification(OrderFilter filter)
    {
        Query.ApplyFilter(filter);

        Query.OrderByDescending(o => o.CreatedAt)
             .Select(o => new OrderResponse(o.Id, o.CustomerId, o.TotalAmount, o.CreatedAt));

        // No Skip/Take here — pagination is handled by repository.GetPagedAsync()
    }
}
```

---

## Step 4 – Create a Single-Item Specification (with Includes)

When you need to load related entities, use `.Include()` instead of returning a projection:

**`src/APITemplate/Application/Specifications/OrderByIdSpecification.cs`**

```csharp
using Ardalis.Specification;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class OrderByIdSpecification : Specification<Order>
{
    public OrderByIdSpecification(Guid id)
    {
        Query.Where(o => o.Id == id)
             .Include(o => o.Items)          // eager load order items
             .AsNoTracking();                // read-only — no change tracking overhead
    }
}
```

> **Tip:** Always use `.AsNoTracking()` for read-only queries to improve performance.

---

## Step 5 – Use the Specifications in the Service

```csharp
public async Task<PagedResponse<OrderResponse>> GetAllAsync(
    OrderFilter filter, CancellationToken ct = default)
{
    return await _repository.GetPagedAsync(
        new OrderSpecification(filter), filter.PageNumber, filter.PageSize, ct);
}

public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    // Using RepositoryBase.FirstOrDefaultAsync with a specification:
    var order = await _repository.FirstOrDefaultAsync(new OrderByIdSpecification(id), ct);
    return order?.ToResponse();
}
```

---

## Available Repository Methods

These are provided by `RepositoryBase<T>` (via Ardalis):

| Method | Description |
|--------|-------------|
| `GetPagedAsync(spec, pageNumber, pageSize, ct)` | Returns paginated results with total count in a single SQL query |
| `ListAsync(spec)` | Returns all matching entities (or projected DTOs) |
| `CountAsync(spec)` | Returns the count of matching entities |
| `FirstOrDefaultAsync(spec)` | Returns first match or `null` |
| `AnyAsync(spec)` | Returns `true` if any match exists |
| `GetByIdAsync(id)` | Finds by primary key (no specification needed) |
| `AddAsync(entity)` | Stages entity for insert |
| `UpdateAsync(entity)` | Stages entity for update |
| `DeleteAsync(id)` | Finds and stages entity for delete |
| `AsQueryable()` | Raw `IQueryable<T>` — used by GraphQL resolvers |

---

## Advanced: Specification by Related Entity

Filter by a navigation property:

```csharp
public sealed class OrdersByCustomerSpecification : Specification<Order, OrderResponse>
{
    public OrdersByCustomerSpecification(Guid customerId)
    {
        Query.Where(o => o.CustomerId == customerId)
             .OrderByDescending(o => o.CreatedAt)
             .Select(o => new OrderResponse(o.Id, o.CustomerId, o.TotalAmount, o.CreatedAt));
    }
}
```

Filter based on a related entity's property (EF Core generates a JOIN):

```csharp
public sealed class ReviewByProductIdSpecification : Specification<ProductReview, ProductReviewResponse>
{
    public ReviewByProductIdSpecification(Guid productId)
    {
        Query.Where(r => r.ProductId == productId)
             .OrderByDescending(r => r.CreatedAt)
             .Select(r => new ProductReviewResponse(
                 r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt));
    }
}
```

---

## Checklist

- [ ] Create `<Entity>FilterCriteria.cs` with `ApplyFilter()` extension method
- [ ] Create `<Entity>Specification.cs` (filter + sort + projection — no Skip/Take)
- [ ] Create single-item specifications as needed (with `.Include()`)
- [ ] Use `repository.GetPagedAsync(spec, pageNumber, pageSize, ct)` in the service
- [ ] No registration needed — specifications are plain classes

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Application/Features/Product/Specifications/ProductSpecification.cs` | Filter + sort + projection example (no Skip/Take) |
| `Application/Features/Product/Specifications/ProductFilterCriteria.cs` | Shared `Where` criteria extension |
| `Application/Features/ProductReview/Specifications/ProductReviewByProductIdSpecification.cs` | Single-relation filter |
| `Infrastructure/Repositories/RepositoryBase.cs` | Base repository — provides `GetPagedAsync` for single-query pagination |
| `Application/Features/Product/Services/ProductService.cs` | Usage of `GetPagedAsync(spec, pageNumber, pageSize, ct)` |

