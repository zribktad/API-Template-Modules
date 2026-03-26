# How to Create a MongoDB Migration

This guide explains how to write and run MongoDB migrations using the **Kot.MongoDB.Migrations** library. Migrations are used to create indexes, rename fields, transform documents, or perform any other one-time database operation.

---

## Overview

MongoDB migrations in this project follow a code-first approach:

```
Migration class (Infrastructure/Migrations/)
  ↓
Registered via Kot.MongoDB.Migrations DI extension
  ↓
Executed automatically at application startup
```

The migration runner tracks which migrations have already been applied by storing a record in a `_migrations` collection inside the configured database. Migrations are executed in version order and are idempotent by design.

---

## Step 1 – Create the Migration Class

Migration files live in `src/APITemplate/Infrastructure/Migrations/`. The class must:

- Inherit from `MongoMigration`
- Pass a **SemVer string** to the base constructor (used for ordering)
- Implement `UpAsync` (apply) and `DownAsync` (rollback)

**`src/APITemplate/Infrastructure/Migrations/M002_AddOrdersCollection.cs`**

```csharp
using Kot.MongoDB.Migrations;
using MongoDB.Driver;

namespace APITemplate.Infrastructure.Migrations;

/// <summary>
/// Creates the orders collection and adds required indexes:
///   - idx_customer : ascending on CustomerId — speeds up customer order lookups
///   - idx_created  : descending on CreatedAt — speeds up time-based ordering
/// </summary>
public sealed class M002_AddOrdersCollection : MongoMigration
{
    // Version string determines execution order.
    // Use SemVer: "1.0.0", "1.1.0", "2.0.0", etc.
    public M002_AddOrdersCollection() : base("1.1.0") { }

    public override async Task UpAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct)
    {
        // Create collection (implicit on first insert, but explicit creation
        // is needed to set collation or capped options)
        await db.CreateCollectionAsync("orders", cancellationToken: ct);

        var collection = db.GetCollection<MongoDB.Bson.BsonDocument>("orders");

        var indexes = new[]
        {
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("CustomerId"),
                new CreateIndexOptions { Name = "idx_customer" }),

            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Descending("CreatedAt"),
                new CreateIndexOptions { Name = "idx_created" })
        };

        await collection.Indexes.CreateManyAsync(session, indexes, cancellationToken: ct);
    }

    public override async Task DownAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct)
    {
        await db.DropCollectionAsync("orders", ct);
    }
}
```

> **Version ordering:** Migrations execute from the lowest to the highest version string. If two migrations have the same version, the order is undefined — always use unique version strings.

---

## Step 2 – Common Migration Patterns

### Add an Index to an Existing Collection

```csharp
public override async Task UpAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
{
    var collection = db.GetCollection<ProductData>("product_data");

    await collection.Indexes.CreateOneAsync(
        session,
        new CreateIndexModel<ProductData>(
            Builders<ProductData>.IndexKeys.Text(x => x.Title),
            new CreateIndexOptions { Name = "idx_title_text" }),
        cancellationToken: ct);
}

public override Task DownAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
{
    var collection = db.GetCollection<ProductData>("product_data");
    return collection.Indexes.DropOneAsync(session, "idx_title_text", ct);
}
```

### Rename a Field in All Documents

```csharp
public override async Task UpAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
{
    var collection = db.GetCollection<MongoDB.Bson.BsonDocument>("product_data");

    var update = Builders<MongoDB.Bson.BsonDocument>.Update
        .Rename("OldFieldName", "NewFieldName");

    await collection.UpdateManyAsync(
        session,
        filter: Builders<MongoDB.Bson.BsonDocument>.Filter.Exists("OldFieldName"),
        update: update,
        cancellationToken: ct);
}
```

### Back-fill a New Field

```csharp
public override async Task UpAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
{
    var collection = db.GetCollection<MongoDB.Bson.BsonDocument>("product_data");

    var update = Builders<MongoDB.Bson.BsonDocument>.Update
        .Set("IsActive", true);

    await collection.UpdateManyAsync(
        session,
        filter: Builders<MongoDB.Bson.BsonDocument>.Filter.Exists("IsActive", exists: false),
        update: update,
        cancellationToken: ct);
}

public override Task DownAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
{
    var collection = db.GetCollection<MongoDB.Bson.BsonDocument>("product_data");

    var update = Builders<MongoDB.Bson.BsonDocument>.Update.Unset("IsActive");
    return collection.UpdateManyAsync(
        session,
        filter: Builders<MongoDB.Bson.BsonDocument>.Filter.Empty,
        update: update,
        cancellationToken: ct);
}
```

---

## Step 3 – Verify Registration

Migrations are discovered automatically via assembly scanning. The registration in `ServiceCollectionExtensions.AddMongoDB()` already handles this:

```csharp
services.AddMongoMigrations(
    mongoSettings.ConnectionString,
    new MigrationOptions(mongoSettings.DatabaseName),
    config => config.LoadMigrationsFromAssembly(typeof(ServiceCollectionExtensions).Assembly));
```

Any `MongoMigration` subclass in the **same assembly** is picked up without additional registration.

---

## Step 4 – Run the Migration

Migrations execute automatically when the application starts. In development, start the app normally:

```bash
dotnet run --project src/APITemplate
```

The startup sequence (`Extensions/ApplicationBuilderExtensions.cs`) calls `UseDatabaseAsync()` which triggers both EF Core and MongoDB migrations before the HTTP pipeline opens.

To verify, check the `_migrations` collection in your MongoDB database:

```js
db._migrations.find()
// { "_id": ..., "Version": "1.0.0", "Name": "M001_CreateProductDataIndexes", "AppliedAt": ... }
// { "_id": ..., "Version": "1.1.0", "Name": "M002_AddOrdersCollection",      "AppliedAt": ... }
```

---

## Naming Convention

| Part | Convention | Example |
|------|-----------|---------|
| Class name | `M<seq>_<PascalDescription>` | `M002_AddOrdersCollection` |
| Version string | SemVer, monotonically increasing | `"1.1.0"` |
| File name | Match class name | `M002_AddOrdersCollection.cs` |

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Infrastructure/Migrations/` | MongoDB migration classes |
| `Infrastructure/Persistence/MongoDbContext.cs` | MongoDB client wrapper |
| `Infrastructure/Persistence/MongoDbSettings.cs` | Connection string / database name from config |
| `Extensions/ServiceCollectionExtensions.cs` | `AddMongoMigrations()` registration |
| `Extensions/ApplicationBuilderExtensions.cs` | `UseDatabaseAsync()` — runs migrations at startup |
| `appsettings.json` → `MongoDB` section | `ConnectionString`, `DatabaseName` |
