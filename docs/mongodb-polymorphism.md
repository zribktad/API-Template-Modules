# How to Add Polymorphic MongoDB Documents

This guide explains how to store multiple subtypes in a single MongoDB collection using the **BSON discriminator** pattern, following the `ProductData` / `ImageProductData` / `VideoProductData` hierarchy already in the project.

---

## Overview

A BSON discriminator is a special field (`_t` by default) that MongoDB stores alongside each document. When reading, the driver inspects `_t` and deserialises the document into the correct C# subtype automatically.

```
product_data collection (MongoDB)
  { _t: "image", title: "...", width: 1920, ... }   → ImageProductData
  { _t: "video", title: "...", duration: 120, ... }  → VideoProductData
  { _t: "audio", title: "...", bitrate: 320, ... }   → AudioProductData  ← new
```

---

## Step 1 – Create the Abstract Base Class

The base class is decorated with `[BsonDiscriminator(RootClass = true)]` and lists all known subtypes via `[BsonKnownTypes]`. This is already done for `ProductData`:

**`src/APITemplate/Domain/Entities/ProductData.cs`** (existing)

```csharp
[BsonDiscriminator(RootClass = true)]
[BsonKnownTypes(typeof(ImageProductData), typeof(VideoProductData))]
public abstract class ProductData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Step 2 – Add a New Subtype

Create the subtype class in `src/APITemplate/Domain/Entities/`. Decorate it with `[BsonDiscriminator]` to set the `_t` value that will be written into MongoDB.

**`src/APITemplate/Domain/Entities/AudioProductData.cs`**

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace APITemplate.Domain.Entities;

[BsonDiscriminator("audio")]
public sealed class AudioProductData : ProductData
{
    public int DurationSeconds { get; set; }

    public int BitrateKbps { get; set; }

    public string Format { get; set; } = string.Empty;    // e.g. "mp3", "flac"

    public long FileSizeBytes { get; set; }
}
```

> The string passed to `[BsonDiscriminator]` is the `_t` value stored in MongoDB. Keep it short and lowercase by convention.

---

## Step 3 – Register the Subtype on the Base Class

Open `ProductData.cs` and add `typeof(AudioProductData)` to the `[BsonKnownTypes]` attribute:

```csharp
[BsonDiscriminator(RootClass = true)]
[BsonKnownTypes(
    typeof(ImageProductData),
    typeof(VideoProductData),
    typeof(AudioProductData))]   // ← add here
public abstract class ProductData { ... }
```

> The driver uses `[BsonKnownTypes]` at startup to register the discriminator mappings. Every subtype must be listed, otherwise deserialization will fail when the driver encounters the `_t` value.

---

## Step 4 – Create the Request DTO

**`src/APITemplate/Application/Features/ProductData/DTOs/CreateAudioProductDataRequest.cs`**

```csharp
namespace APITemplate.Application.DTOs.Requests;

public sealed record CreateAudioProductDataRequest(
    string Title,
    string? Description,
    int DurationSeconds,
    int BitrateKbps,
    string Format,
    long FileSizeBytes
);
```

---

## Step 5 – Add a FluentValidation Validator

**`src/APITemplate/Application/Features/ProductData/Validation/CreateAudioProductDataRequestValidator.cs`**

```csharp
using FluentValidation;

namespace APITemplate.Application.Validators;

public sealed class CreateAudioProductDataRequestValidator
    : AbstractValidator<CreateAudioProductDataRequest>
{
    public CreateAudioProductDataRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DurationSeconds).GreaterThan(0);
        RuleFor(x => x.BitrateKbps).GreaterThan(0);
        RuleFor(x => x.Format).NotEmpty().MaximumLength(10);
        RuleFor(x => x.FileSizeBytes).GreaterThan(0);
    }
}
```

---

## Step 6 – Add the Mapping

Open `src/APITemplate/Application/Features/ProductData/Mappings/ProductDataMappings.cs` and add a mapping for the new subtype:

```csharp
// Existing pattern (in ProductDataMappings.cs):
public static ProductDataResponse ToResponse(this ProductData data)
{
    return data switch
    {
        ImageProductData img => new ProductDataResponse(img.Id, img.Title, img.Description,
            "image", img.CreatedAt),
        VideoProductData vid => new ProductDataResponse(vid.Id, vid.Title, vid.Description,
            "video", vid.CreatedAt),
        AudioProductData aud => new ProductDataResponse(aud.Id, aud.Title, aud.Description,
            "audio", aud.CreatedAt),   // ← add here
        _ => new ProductDataResponse(data.Id, data.Title, data.Description, "unknown", data.CreatedAt)
    };
}
```

---

## Step 7 – Add the Service Method

Open `src/APITemplate/Application/Features/ProductData/Services/ProductDataService.cs` and add:

```csharp
public async Task<ProductDataResponse> CreateAudioAsync(
    CreateAudioProductDataRequest request,
    CancellationToken ct = default)
{
    var entity = new AudioProductData
    {
        Title           = request.Title,
        Description     = request.Description,
        DurationSeconds = request.DurationSeconds,
        BitrateKbps     = request.BitrateKbps,
        Format          = request.Format,
        FileSizeBytes   = request.FileSizeBytes
    };

    var created = await _repository.CreateAsync(entity, ct);
    return created.ToResponse();
}
```

Also add the method to the `IProductDataService` interface.

---

## Step 8 – Add the Controller Action

Open `src/APITemplate/Api/Controllers/V1/ProductDataController.cs` and add:

```csharp
[HttpPost("audio")]
public async Task<ActionResult<ProductDataResponse>> CreateAudio(
    CreateAudioProductDataRequest request, CancellationToken ct)
{
    var created = await _service.CreateAudioAsync(request, ct);
    return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1.0" }, created);
}
```

---

## Step 9 – Add a MongoDB Migration (Optional but Recommended)

If the new subtype requires a new index or collection setup, create a migration. See [mongodb-migration.md](mongodb-migration.md) for the full workflow.

For example, to add an index on `BitrateKbps`:

```csharp
public sealed class M003_AddAudioBitrateIndex : MongoMigration
{
    public M003_AddAudioBitrateIndex() : base("1.2.0") { }

    public override async Task UpAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
    {
        var collection = db.GetCollection<ProductData>("product_data");

        await collection.Indexes.CreateOneAsync(
            session,
            new CreateIndexModel<ProductData>(
                Builders<ProductData>.IndexKeys.Ascending("BitrateKbps"),
                new CreateIndexOptions { Name = "idx_bitrate",
                    PartialFilterExpression = Builders<ProductData>.Filter.Eq("_t", "audio") }),
            cancellationToken: ct);
    }

    public override Task DownAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
    {
        var collection = db.GetCollection<ProductData>("product_data");
        return collection.Indexes.DropOneAsync(session, "idx_bitrate", ct);
    }
}
```

---

## How the Discriminator Works at Runtime

**Inserting a document:**

```json
// AudioProductData serialised by the MongoDB driver
{
  "_id":  ObjectId("..."),
  "_t":   "audio",           ← discriminator set by [BsonDiscriminator("audio")]
  "Title": "My Podcast",
  "DurationSeconds": 3600,
  "BitrateKbps": 128,
  "Format": "mp3",
  "FileSizeBytes": 57600000,
  "CreatedAt": ISODate("...")
}
```

**Querying by type:**

```csharp
// Filter by discriminator value — returns only AudioProductData documents
var filter = Builders<ProductData>.Filter.Eq("_t", "audio");
var audios = await _collection.Find(filter).ToListAsync();
```

The driver deserialises each result into `AudioProductData` automatically.

---

## Checklist

- [ ] Create subtype class in `Domain/Entities/` with `[BsonDiscriminator("…")]`
- [ ] Add the subtype to `[BsonKnownTypes]` on the base class
- [ ] Create request DTO in `Application/Features/ProductData/DTOs/`
- [ ] Create validator in `Application/Features/ProductData/Validation/`
- [ ] Add mapping in `Application/Features/ProductData/Mappings/ProductDataMappings.cs`
- [ ] Add service method in `Application/Features/ProductData/Services/ProductDataService.cs`
- [ ] Add controller action in `Api/Controllers/V1/ProductDataController.cs`
- [ ] (Optional) Add a MongoDB migration for indexes

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Domain/Entities/ProductData.cs` | Abstract base with `[BsonDiscriminator(RootClass = true)]` |
| `Domain/Entities/ImageProductData.cs` | Example concrete subtype |
| `Domain/Entities/VideoProductData.cs` | Example concrete subtype |
| `Infrastructure/Persistence/MongoDbContext.cs` | MongoDB client & collection access |
| `Infrastructure/Repositories/ProductDataRepository.cs` | CRUD operations on the collection |
| `Application/Features/ProductData/Services/ProductDataService.cs` | Business logic for all subtypes |
| `Api/Controllers/V1/ProductDataController.cs` | HTTP endpoints per subtype |
| `Infrastructure/Migrations/` | MongoDB index migrations |

