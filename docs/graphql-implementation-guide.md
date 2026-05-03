# 🚀 Complete GraphQL Implementation Guide (HotChocolate)

This document explains the GraphQL implementation in this project, from foundational concepts to advanced patterns used in our modular monolith. It is designed for developers who want to understand both the syntax and the underlying architecture.

---

## 1. Architecture: Why this way?

We use a **Code-First** approach. The schema is generated from C# code, providing full type safety and seamless integration with .NET features.

### Modular Schema Stitching
In a modular monolith (Identity, Catalog, Reviews, etc.), we avoid a single massive schema file. Instead, we use **Type Extensions**:
- Each module defines its own `Query` and `Mutation` extensions.
- At application startup, these "slices" are merged into a single unified GraphQL schema.
- This ensures that modules remain decoupled while still contributing to the global API.

---

## 2. Folder Structure & Conventions

Each module maintains a consistent structure within its `GraphQL/` folder:

| Folder | Example File | Purpose |
| :--- | :--- | :--- |
| **`Queries/`** | `ProductQueries.cs` | Defines entry points for reading data (Read-only). |
| **`Mutations/`** | `ProductMutations.cs` | Defines entry points for data modification (Create/Update/Delete). |
| **`Types/`** | `ProductType.cs` | Configures how C# objects are mapped to the GraphQL schema. |
| **`DataLoaders/`** | `CategoryByIdDataLoader.cs` | Batching logic to solve the N+1 performance problem. |
| **`Models/`** | `ProductQueryInput.cs` | GraphQL-specific models (inputs, custom filter objects, paginated results). |

---

## 3. Type Definitions (`ObjectType<T>`)

Classes inheriting from `ObjectType<T>` act as the "map" for the schema. 

### Why not expose DTOs directly?
1. **Abstraction:** You can name fields differently in GraphQL than in C#.
2. **Computed Fields:** Add fields that don't exist in the database but are calculated at runtime (e.g., `isAvailable`, `discountedPrice`).
3. **Documentation:** Use `.Description()` to provide inline documentation for frontend developers.

### Real-world Example: `ProductType`
```csharp
public sealed class ProductType : ObjectType<ProductResponse>
{
    protected override void Configure(IObjectTypeDescriptor<ProductResponse> descriptor)
    {
        descriptor.Description("Represents a tangible item in the catalog.");

        descriptor.Field(p => p.Id).Type<NonNullType<UuidType>>();
        
        // Computed Field Example: Not in the database/DTO
        descriptor.Field("isPremium")
            .Description("Returns true if the product price is over 1000.")
            .Resolve(context => context.Parent<ProductResponse>().Price > 1000);

        // Relation Example: Connecting Product to Category via a Resolver
        descriptor.Field("category")
            .Description("The category this product belongs to.")
            .ResolveWith<ProductTypeResolvers>(r => r.GetCategory(default!, default!, default));
    }
}
```

---

## 4. Resolvers: The "Data Fetchers"

A resolver is a method responsible for providing data for a specific field.

### Key Parameter Attributes:
- **`[Parent]`**: Provides access to the object currently being processed. If you are resolving a "Category" for a "Product", `[Parent]` gives you the `Product` object.
- **`[Service]`**: Injects services from the DI container (e.g., `IMessageBus` for CQRS).
- **`CancellationToken`**: Always include this. It allows the server to stop processing if the client cancels the request.

### Real-world Example: `ProductTypeResolvers`
```csharp
public sealed class ProductTypeResolvers
{
    // Fetches the category for a specific product
    public async Task<CategoryResponse?> GetCategory(
        [Parent] ProductResponse product,
        CategoryByIdDataLoader dataLoader,
        CancellationToken ct)
    {
        if (product.CategoryId is null) return null;
        
        // Use the DataLoader to avoid N+1 queries!
        return await dataLoader.LoadAsync(product.CategoryId.Value, ct);
    }
}
```

---

## 5. Performance: DataLoaders (Solve N+1)

The N+1 problem occurs when fetching a list of 100 items requires 1 query for the list and 100 additional queries for a related property (like Category).

**How DataLoaders work:**
1. As HotChocolate processes the list of products, it "collects" all unique Category IDs.
2. It pauses until all IDs for that level are gathered.
3. It calls the DataLoader's `BatchAsync` method with the list of IDs.
4. The DataLoader executes **ONE** SQL query: `SELECT * FROM Categories WHERE Id IN (...)`.
5. Results are distributed back to the individual items.

### Real-world Example: `CategoryByIdDataLoader`
```csharp
public sealed class CategoryByIdDataLoader(
    IMessageBus bus, // Using Wolverine bus to fetch data from the Identity/Catalog module
    IBatchScheduler batchScheduler,
    DataLoaderOptions? options = null)
    : BatchDataLoader<Guid, CategoryResponse>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<Guid, CategoryResponse>> LoadBatchAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken ct)
    {
        // Dispatch a query to the message bus to fetch multiple categories at once
        var result = await bus.InvokeAsync<IEnumerable<CategoryResponse>>(
            new GetCategoriesByIdsQuery(keys), ct);

        return result.ToDictionary(x => x.Id);
    }
}
```

---

## 6. Mutations: Writing Data

Mutations follow the same CQRS pattern as our REST API. They dispatch commands via the `IMessageBus` and return a result.

### Real-world Example: `ProductMutations`
```csharp
[ExtendObjectType(OperationTypeNames.Mutation)]
public sealed class ProductMutations
{
    [Authorize(Roles = new[] { "Admin" })]
    public async Task<ErrorOr<ProductResponse>> CreateProduct(
        CreateProductInput input,
        [Service] IMessageBus bus,
        CancellationToken ct)
    {
        // 1. Map input to Command
        var command = new CreateProductCommand(input.Name, input.Price, input.CategoryId);

        // 2. Dispatch via Wolverine and return the result
        // The .ToGraphQLResult() extension handles error mapping automatically
        return await bus.InvokeAsync<ErrorOr<ProductResponse>>(command, ct);
    }
}
```

---

## 7. Result Pattern Integration

Our handlers return `ErrorOr<T>`. We use extension methods to map these to GraphQL:
- **`.ToGraphQLResult()`**: Converts `Success` to data and `Error` to a `GraphQLException` (which HotChocolate returns in the `errors` array).
- **`.ToGraphQLNullableResult()`**: Best for optional relations. If data is missing or an error occurs, it returns `null` instead of failing the whole query.

---

## 8. Security & DoS Protection

GraphQL is uniquely vulnerable to complex, resource-heavy queries. We use multiple layers of defense:

1.  **Execution Depth (`MaxExecutionDepth`):** Limits how deep a query can go (e.g., max 5 levels). Prevents "recursive" queries that could crash the server.
2.  **Field Cost (`MaxFieldCost`):** Each field has a "cost". Complex queries with many aliases or nested fields are rejected if they exceed a total cost threshold.
3.  **Mandatory Paging:** All list-returning fields must implement paging. We enforce a `MaxPageSize` (e.g., 100) to prevent pulling the entire database into memory.
4.  **Authorization:** We use standard `[Authorize]` attributes on Queries, Mutations, or even individual fields.

These limits are centrally configured in `GraphQLConstants.cs` and applied in `GraphQLServiceCollectionExtensions.cs`.

---

## 9. How to add a new GraphQL Feature? (Step-by-Step)

1. **Define the Model:** Create a `Response` DTO or an `Input` model if needed.
2. **Create the Type:** Inherit from `ObjectType<T>` to configure the schema representation.
3. **Register Query/Mutation:** Add a method to the module's `Query` or `Mutation` extension class.
4. **Implement Resolver:** If the field requires a relation, add logic to the module's `Resolvers` class.
5. **Use DataLoader:** If fetching database records for a relation, implement a `BatchDataLoader`.
6. **DI Registration:** Register your new types and loaders in the module's entry point (e.g., `ProductCatalogModule.cs`):
   ```csharp
   .AddType<MyNewType>()
   .AddTypeExtension<MyNewQueries>()
   .AddDataLoader<MyNewDataLoader>()
   ```

---

## Why is it so structured?
While it might seem like a lot of classes, this architecture guarantees:
- **Performance:** No unnecessary database round-trips (via DataLoaders).
- **Security:** Strict validation and depth control.
- **Maintainability:** Each module owns its schema slice, preventing a "big ball of mud" API.
