# How to Create a GraphQL Endpoint

This guide walks through the full workflow for adding a new entity to the GraphQL API using HotChocolate. The example below extends the template with an `Order` entity, mirroring the pattern already used for `Product` and `ProductReview`.

---

## Overview

The GraphQL stack is built with **HotChocolate 15** and lives inside `src/APITemplate/Api/GraphQL/`. The pipeline for every entity is:

```
Domain entity
  → GraphQL Type  (Api/GraphQL/Types/)
  → Query class   (Api/GraphQL/Queries/)
  → Mutation class (Api/GraphQL/Mutations/)
  → [optional] DataLoader (Api/GraphQL/DataLoaders/)
  → Registration in ServiceCollectionExtensions.cs
```

---

## Step 1 – Create the GraphQL Type

A *type class* maps domain-entity properties to GraphQL field descriptors and adds descriptions visible in the schema.

**`src/APITemplate/Api/GraphQL/Types/OrderType.cs`**

```csharp
using APITemplate.Domain.Entities;

namespace APITemplate.Api.GraphQL.Types;

public sealed class OrderType : ObjectType<Order>
{
    protected override void Configure(IObjectTypeDescriptor<Order> descriptor)
    {
        descriptor.Description("Represents a customer order.");

        descriptor.Field(o => o.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the order.");

        descriptor.Field(o => o.CustomerId)
            .Type<NonNullType<UuidType>>()
            .Description("The customer who placed the order.");

        descriptor.Field(o => o.TotalAmount)
            .Type<NonNullType<DecimalType>>()
            .Description("The total monetary value of the order.");

        descriptor.Field(o => o.CreatedAt)
            .Description("The UTC timestamp of when the order was created.");
    }
}
```

> **Tip:** Only list fields you want exposed. Omitting a field keeps it out of the public schema.

---

## Step 2 – Create the Query Class

Queries return data. Use the HotChocolate middleware attributes to add automatic paging, filtering, sorting, and projection (EF Core pushes these into SQL automatically).

**`src/APITemplate/Api/GraphQL/Queries/OrderQueries.cs`**

```csharp
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Api.GraphQL.Queries;

public class OrderQueries
{
    // Returns a paged connection — client sends `first`/`after` or `last`/`before`.
    [UsePaging(MaxPageSize = 100, DefaultPageSize = 20)]
    [UseProjection]   // EF Core generates SELECT only for requested fields
    [UseFiltering]    // Adds `where` argument to the query
    [UseSorting]      // Adds `order` argument to the query
    public IQueryable<Order> GetOrders([Service] IOrderRepository repo)
        => repo.AsQueryable();

    // Returns a single item or null.
    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Order> GetOrderById(
        Guid id,
        [Service] IOrderRepository repo)
        => repo.AsQueryable().Where(o => o.Id == id);
}
```

> **Why `IQueryable`?** Returning `IQueryable` lets HotChocolate translate the client's `where`/`order`/field-selection into a single optimised SQL query rather than loading everything in memory.

---

## Step 3 – Create the Mutation Class

Mutations change state. Inject the relevant application service and delegate all business logic to it. Use `[Authorize]` to require a valid JWT token.

**`src/APITemplate/Api/GraphQL/Mutations/OrderMutations.cs`**

```csharp
using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using HotChocolate.Authorization;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
public class OrderMutations
{
    public async Task<OrderResponse> CreateOrder(
        CreateOrderRequest input,
        [Service] IOrderService orderService,
        CancellationToken ct)
    {
        return await orderService.CreateAsync(input, ct);
    }

    public async Task<bool> DeleteOrder(
        Guid id,
        [Service] IOrderService orderService,
        CancellationToken ct)
    {
        await orderService.DeleteAsync(id, ct);
        return true;
    }
}
```

---

## Step 4 – Register Everything in `ServiceCollectionExtensions.cs`

Open `src/APITemplate/Extensions/ServiceCollectionExtensions.cs` and extend `AddGraphQLConfiguration()`:

```csharp
public static IServiceCollection AddGraphQLConfiguration(this IServiceCollection services)
{
    services
        .AddGraphQLServer()
        // --- existing registrations ---
        .AddQueryType<ProductQueries>()
        .AddTypeExtension<ProductReviewQueries>()
        .AddMutationType<ProductMutations>()
        .AddTypeExtension<ProductReviewMutations>()
        .AddType<ProductType>()
        .AddType<ProductReviewType>()
        // --- new registrations ---
        .AddQueryType<OrderQueries>()          // use AddTypeExtension<OrderQueries>() if defined as a type extension
        .AddMutationType<OrderMutations>()     // use AddTypeExtension<OrderMutations>() if defined as a type extension
        .AddType<OrderType>()
        // --- middleware ---
        .AddAuthorization()
        .AddProjections()
        .AddFiltering()
        .AddSorting()
        .ModifyPagingOptions(o =>
        {
            o.MaxPageSize = 100;
            o.DefaultPageSize = 20;
            o.IncludeTotalCount = true;
        })
        .AddMaxExecutionDepthRule(5);

    return services;
}
```

> **`AddTypeExtension` vs `AddQueryType`:** Use `AddQueryType` for the *first* query class; use `AddTypeExtension` for all subsequent ones. The same rule applies to mutations.

---

## Step 5 (Optional) – Add a DataLoader for Nested Collections

A DataLoader prevents the **N+1 problem** when a query fetches a collection of parents and then loads a child collection for each. Without it, 100 orders would trigger 101 queries (1 for orders + 1 per order for its items).

**`src/APITemplate/Api/GraphQL/DataLoaders/OrderItemsByOrderDataLoader.cs`**

```csharp
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Api.GraphQL.DataLoaders;

public sealed class OrderItemsByOrderDataLoader : BatchDataLoader<Guid, OrderItem[]>
{
    private readonly IOrderItemRepository _repo;

    public OrderItemsByOrderDataLoader(
        IOrderItemRepository repo,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options = default!)
        : base(batchScheduler, options)
    {
        _repo = repo;
    }

    protected override async Task<IReadOnlyDictionary<Guid, OrderItem[]>> LoadBatchAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken ct)
    {
        // One SQL query for ALL order IDs at once
        var items = await _repo.AsQueryable()
            .Where(i => orderIds.Contains(i.OrderId))
            .ToListAsync(ct);

        var lookup = items.ToLookup(i => i.OrderId);

        return orderIds
            .Distinct()
            .ToDictionary(id => id, id => lookup[id].ToArray());
    }
}
```

Then wire the DataLoader inside `OrderType.cs`:

```csharp
// In OrderType.Configure():
descriptor.Field(o => o.Items)
    .ResolveWith<OrderTypeResolvers>(r => r.GetItems(default!, default!))
    .Description("The line items belonging to this order.");

// Resolver class (in the same file or a separate one):
internal sealed class OrderTypeResolvers
{
    public Task<OrderItem[]> GetItems(
        [Parent] Order order,
        OrderItemsByOrderDataLoader loader)
        => loader.LoadAsync(order.Id);
}
```

Register the DataLoader in `AddGraphQLConfiguration()`:

```csharp
.AddDataLoader<OrderItemsByOrderDataLoader>()
```

> **When is a DataLoader NOT needed?** If your query resolver returns `IQueryable` with `[UseProjection]`, EF Core builds a JOIN automatically — no DataLoader required. Use a DataLoader only when the child data comes from a different source or when you use a manual resolver.

---

## Step 6 – Try It Out

Start the application and open the Nitro playground at `/graphql/ui`.

**Query example:**

```graphql
query {
  orders(
    first: 10
    where: { totalAmount: { gte: 100 } }
    order: { createdAt: DESC }
  ) {
    nodes {
      id
      customerId
      totalAmount
      createdAt
    }
    totalCount
    pageInfo {
      hasNextPage
      endCursor
    }
  }
}
```

**Mutation example (requires `Authorization: Bearer <token>` header):**

```graphql
mutation {
  createOrder(input: {
    customerId: "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    totalAmount: 199.99
  }) {
    id
    createdAt
  }
}
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Api/GraphQL/Types/` | GraphQL schema type definitions |
| `Api/GraphQL/Queries/` | Read resolvers |
| `Api/GraphQL/Mutations/` | Write resolvers |
| `Api/GraphQL/DataLoaders/` | N+1 prevention helpers |
| `Extensions/ServiceCollectionExtensions.cs` | Central DI / GraphQL registration |
| `Program.cs` | `app.MapGraphQL()` and `app.MapNitroApp("/graphql/ui")` |
