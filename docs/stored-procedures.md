# How to Add a Stored Procedure (PostgreSQL Function)

This guide walks through the full workflow for adding a new PostgreSQL function and calling it from the application using the `IStoredProcedure<T>` + `IStoredProcedureExecutor` pattern.

---

## Overview

The stored procedure pipeline in this project is:

```
SQL function (.sql file, embedded resource)
  → EF Core migration (applies the function to the database)
  → Keyless result entity (Domain/Entities/)
  → EF HasNoKey() configuration (Infrastructure/Persistence/Configurations/)
  → Procedure record (Infrastructure/StoredProcedures/)
  → Repository method (Infrastructure/Repositories/)
  → Service method (Application/Features/Category/Services/)
  → Controller action (Api/Controllers/V1/)
```

The `StoredProcedureExecutor` uses `DbContext.Set<T>().FromSql(procedure.ToSql())`, which automatically **parameterises** all interpolated values to help prevent SQL injection. Avoid concatenating raw SQL fragments or identifiers into the command text; always use the provided APIs and interpolation only for values.

---

## Step 1 – Write the SQL Function

Create a `.sql` file in `src/APITemplate/Infrastructure/Database/Functions/`.

**`src/APITemplate/Infrastructure/Database/Functions/get_order_summary.sql`**

```sql
CREATE FUNCTION get_order_summary(p_customer_id UUID)
RETURNS TABLE(
    "CustomerId"   UUID,
    "OrderCount"   BIGINT,
    "TotalSpent"   NUMERIC,
    "LastOrderAt"  TIMESTAMP WITH TIME ZONE
)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        o."CustomerId",
        COUNT(o."Id"),
        COALESCE(SUM(o."TotalAmount"), 0),
        MAX(o."CreatedAt")
    FROM "Orders" o
    WHERE o."CustomerId" = p_customer_id
    GROUP BY o."CustomerId";
END;
$$;
```

> **Column names** must match (case-insensitively) the C# property names on the result entity. EF Core maps by name.

---

## Step 2 – Mark the File as an Embedded Resource

In `APITemplate.csproj`, add:

```xml
<ItemGroup>
  <EmbeddedResource Include="Infrastructure\Database\Functions\get_order_summary.sql" />
</ItemGroup>
```

The helper `SqlResource.Load("get_order_summary.sql")` reads the file from the compiled assembly at runtime, so it works after `dotnet publish` without requiring a file system path.

---

## Step 3 – Apply the Function via an EF Core Migration

```bash
dotnet ef migrations add AddGetOrderSummaryFunction \
    --project src/APITemplate \
    --output-dir Migrations
```

Open the generated migration and replace the empty `Up`/`Down` with:

```csharp
using APITemplate.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddGetOrderSummaryFunction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(SqlResource.Load("get_order_summary.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_order_summary(UUID);");
    }
}
```

Apply the migration:

```bash
dotnet ef database update --project src/APITemplate
```

---

## Step 4 – Create the Keyless Result Entity

The result entity has no primary key and no backing table — it only exists so EF Core can materialise rows returned by the function.

**`src/APITemplate/Domain/Entities/OrderSummary.cs`**

```csharp
namespace APITemplate.Domain.Entities;

/// <summary>
/// Keyless entity — result set of the get_order_summary() PostgreSQL function.
/// </summary>
public sealed class OrderSummary
{
    public Guid CustomerId { get; set; }
    public long OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime LastOrderAt { get; set; }
}
```

---

## Step 5 – Register the Entity as Keyless in EF Core

**`src/APITemplate/Infrastructure/Persistence/Configurations/OrderSummaryConfiguration.cs`**

```csharp
using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

public sealed class OrderSummaryConfiguration : IEntityTypeConfiguration<OrderSummary>
{
    public void Configure(EntityTypeBuilder<OrderSummary> builder)
    {
        builder.HasNoKey();

        // ExcludeFromMigrations prevents EF from creating a table for this type.
        builder.ToTable("OrderSummary", t => t.ExcludeFromMigrations());
    }
}
```

`AppDbContext` picks up the configuration automatically via `ApplyConfigurationsFromAssembly` — no changes to `AppDbContext` are needed.

---

## Step 6 – Create the Procedure Record

A procedure record encapsulates the SQL template and its parameters. It implements `IStoredProcedure<TResult>`.

**`src/APITemplate/Infrastructure/StoredProcedures/GetOrderSummaryProcedure.cs`**

```csharp
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Infrastructure.StoredProcedures;

/// <summary>
/// Calls the <c>get_order_summary(p_customer_id)</c> PostgreSQL function.
/// EF Core converts each interpolated argument into a named SQL parameter
/// (@p0, @p1, ...) — SQL injection is not possible.
/// </summary>
public sealed record GetOrderSummaryProcedure(Guid CustomerId)
    : IStoredProcedure<OrderSummary>
{
    public FormattableString ToSql() =>
        $"SELECT * FROM get_order_summary({CustomerId})";
}
```

---

## Step 7 – Add a Repository Method

Open (or create) the repository for the relevant entity and inject `IStoredProcedureExecutor`:

**`src/APITemplate/Infrastructure/Repositories/OrderRepository.cs`** (excerpt)

```csharp
public sealed class OrderRepository : RepositoryBase<Order>, IOrderRepository
{
    private readonly IStoredProcedureExecutor _spExecutor;

    public OrderRepository(AppDbContext dbContext, IStoredProcedureExecutor spExecutor)
        : base(dbContext)
    {
        _spExecutor = spExecutor;
    }

    public Task<OrderSummary?> GetSummaryAsync(Guid customerId, CancellationToken ct = default)
    {
        return _spExecutor.QueryFirstAsync(new GetOrderSummaryProcedure(customerId), ct);
    }
}
```

Add the method to the repository interface:

```csharp
// Domain/Interfaces/IOrderRepository.cs
public interface IOrderRepository : IRepository<Order>
{
    Task<OrderSummary?> GetSummaryAsync(Guid customerId, CancellationToken ct = default);
}
```

---

## Step 8 – Expose via a Service Method

```csharp
// Application/Features/Category/Services/CategoryService.cs (new method)
public async Task<OrderSummaryResponse?> GetSummaryAsync(Guid customerId, CancellationToken ct)
{
    var summary = await _orderRepository.GetSummaryAsync(customerId, ct);
    return summary?.ToResponse();
}
```

---

## Step 9 – Add the Controller Action

```csharp
// Api/Controllers/V1/OrdersController.cs (new action)
[HttpGet("customers/{customerId:guid}/summary")]
public async Task<ActionResult<OrderSummaryResponse>> GetSummary(Guid customerId, CancellationToken ct)
{
    var summary = await _orderService.GetSummaryAsync(customerId, ct);
    return summary is null ? NotFound() : Ok(summary);
}
```

---

## How `StoredProcedureExecutor` Works

```csharp
// Infrastructure/StoredProcedures/StoredProcedureExecutor.cs
public Task<TResult?> QueryFirstAsync<TResult>(
    IStoredProcedure<TResult> procedure,
    CancellationToken ct = default)
    where TResult : class
{
    return _dbContext.Set<TResult>()
        .FromSql(procedure.ToSql())   // FormattableString → parameterised SQL
        .FirstOrDefaultAsync(ct);
}
```

`FromSql(FormattableString)` is safe against SQL injection because each `{value}` in the interpolated string becomes a `@p0` / `@p1` SQL parameter, never a raw string concatenation.

---

## Checklist

- [ ] Write `.sql` file in `Infrastructure/Database/Functions/`
- [ ] Mark file as `<EmbeddedResource>` in `.csproj`
- [ ] Create EF Core migration referencing `SqlResource.Load()`
- [ ] Apply migration (`dotnet ef database update`)
- [ ] Create keyless result entity in `Domain/Entities/`
- [ ] Add `HasNoKey()` + `ExcludeFromMigrations()` configuration
- [ ] Create `IStoredProcedure<T>` record in `Infrastructure/StoredProcedures/`
- [ ] Add repository method
- [ ] Add service method
- [ ] Add controller action

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Infrastructure/Database/Functions/` | Embedded SQL function definitions |
| `Infrastructure/Database/SqlResource.cs` | Loads embedded `.sql` files |
| `Infrastructure/StoredProcedures/StoredProcedureExecutor.cs` | Executes procedures via EF Core `FromSql` |
| `Infrastructure/StoredProcedures/GetProductCategoryStatsProcedure.cs` | Real example |
| `Domain/Interfaces/IStoredProcedure.cs` | Procedure contract |
| `Domain/Interfaces/IStoredProcedureExecutor.cs` | Executor contract |
| `Domain/Entities/ProductCategoryStats.cs` | Real keyless result entity example |
| `Infrastructure/Persistence/Configurations/ProductCategoryStatsConfiguration.cs` | Real `HasNoKey()` example |

