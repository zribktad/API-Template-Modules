# How to Create an EF Core Migration (PostgreSQL)

This guide explains how to add, apply, and roll back Entity Framework Core migrations against the PostgreSQL database. The project uses **EF Core 10** with the **Npgsql** provider.

---

## Prerequisites

- .NET SDK installed (`dotnet --version`)
- PostgreSQL running (locally or via `docker-compose up -d postgres`)
- `appsettings.Development.json` configured with a valid `DefaultConnection` string

---

## Step 1 – Modify the Domain Entity or Add a New One

All EF-tracked entities live in `src/APITemplate/Domain/Entities/`. Add or change a property:

```csharp
// Domain/Entities/Product.cs
public string? Sku { get; set; }   // ← new property
```

---

## Step 2 – Update the Entity Configuration (if needed)

Entity type configurations live in `src/APITemplate/Infrastructure/Persistence/Configurations/`. Open (or create) the relevant file and express any constraints:

```csharp
// Infrastructure/Persistence/Configurations/ProductConfiguration.cs
builder.Property(p => p.Sku)
    .HasMaxLength(50)
    .IsRequired(false);

builder.HasIndex(p => p.Sku)
    .IsUnique()
    .HasFilter("\"Sku\" IS NOT NULL");  // partial index — nulls not indexed
```

The `AppDbContext` (`Infrastructure/Persistence/AppDbContext.cs`) picks up the configuration automatically via `ApplyConfigurationsFromAssembly`.

---

## Step 3 – Add the Migration

Run from the solution root (where `APITemplate.slnx` lives) or from the project folder.  
The `--project` flag points to the project that owns `AppDbContext`.

```bash
dotnet ef migrations add <MigrationName> \
    --project src/APITemplate \
    --output-dir Migrations
```

**Example:**

```bash
dotnet ef migrations add AddSkuToProduct \
    --project src/APITemplate \
    --output-dir Migrations
```

This generates two files in `src/APITemplate/Migrations/`:

| File | Purpose |
|------|---------|
| `<timestamp>_AddSkuToProduct.cs` | `Up()` / `Down()` migration logic |
| `<timestamp>_AddSkuToProduct.Designer.cs` | EF metadata snapshot (do not edit) |

It also updates `AppDbContextModelSnapshot.cs`.

---

## Step 4 – Review the Generated Migration

Always open the generated migration and verify EF inferred the right SQL. For the `Sku` example:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(
        name: "Sku",
        table: "Products",
        type: "character varying(50)",
        maxLength: 50,
        nullable: true);

    migrationBuilder.CreateIndex(
        name: "IX_Products_Sku",
        table: "Products",
        column: "Sku",
        unique: true,
        filter: "\"Sku\" IS NOT NULL");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex(name: "IX_Products_Sku", table: "Products");
    migrationBuilder.DropColumn(name: "Sku", table: "Products");
}
```

---

## Step 5 – Embed SQL Resources (Optional)

If the migration needs to create or drop a PostgreSQL function / stored procedure, embed the SQL as a resource file and load it with `SqlResource.Load()`:

1. Create `src/APITemplate/Infrastructure/Database/Functions/my_function.sql`
2. Set **Build Action → Embedded Resource** in your IDE (or add to `.csproj`):

```xml
<ItemGroup>
  <EmbeddedResource Include="Infrastructure\Database\Functions\my_function.sql" />
</ItemGroup>
```

3. Call it from the migration:

```csharp
migrationBuilder.Sql(SqlResource.Load("my_function.sql"));
```

4. Drop it in `Down()`:

```csharp
migrationBuilder.Sql("DROP FUNCTION IF EXISTS my_function(UUID);");
```

See `Migrations/20260302153430_AddCategory.cs` for a real example.

---

## Step 6 – Apply the Migration

Migrations are applied automatically at application startup via `UseDatabaseAsync()` in `Extensions/ApplicationBuilderExtensions.cs`. You can also apply them manually:

```bash
dotnet ef database update \
    --project src/APITemplate
```

To apply up to a specific migration:

```bash
dotnet ef database update AddSkuToProduct \
    --project src/APITemplate
```

---

## Step 7 – Roll Back a Migration

To undo the last migration (database must be reverted first):

```bash
# Revert the database to the previous migration
dotnet ef database update <PreviousMigrationName> \
    --project src/APITemplate

# Remove the pending migration files
dotnet ef migrations remove \
    --project src/APITemplate
```

---

## Step 8 – Generate a SQL Script (CI / Production Deployments)

For environments where running EF tooling directly is not appropriate, generate an idempotent SQL script:

```bash
dotnet ef migrations script --idempotent \
    --project src/APITemplate \
    --output migration.sql
```

Apply `migration.sql` through your preferred DB deployment pipeline.

---

## Checklist for a New Entity

- [ ] Create entity class in `Domain/Entities/`
- [ ] Create configuration class in `Infrastructure/Persistence/Configurations/`
- [ ] Add `DbSet<T>` to `AppDbContext`
- [ ] Register the repository in `ServiceCollectionExtensions.AddPersistence()`
- [ ] Run `dotnet ef migrations add <Name>`
- [ ] Review the generated migration file
- [ ] Run `dotnet ef database update` (or let startup auto-migrate)

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Domain/Entities/` | EF-tracked entity classes |
| `Infrastructure/Persistence/AppDbContext.cs` | EF DbContext with DbSets |
| `Infrastructure/Persistence/Configurations/` | Fluent API table/column configuration |
| `Migrations/` | Auto-generated migration history |
| `Infrastructure/Database/SqlResource.cs` | Helper to load embedded `.sql` files |
| `Infrastructure/Database/Functions/` | Embedded SQL scripts |
| `Extensions/ApplicationBuilderExtensions.cs` | `UseDatabaseAsync()` — auto-migrate on startup |
