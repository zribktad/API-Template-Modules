# ProductCatalog Module — AGENTS.md

## OVERVIEW
Products, categories, polymorphic MongoDB media metadata, GraphQL API (HotChocolate). Dual-database module: PostgreSQL (EF Core) + MongoDB (polymorphic documents).

## STRUCTURE
```
ProductCatalog/
├── Entities/               # Category, Product, ProductData (abstract), ProductDataLink ← NOT in Domain/
├── Domain/Services/        # Only services here — entities are at root Entities/
├── Features/               # Product/ and Category/ vertical slices
│   ├── Product/
│   │   ├── Commands/
│   │   ├── Queries/
│   │   ├── Handlers/
│   │   ├── Validators/
│   │   └── Shared/
│   └── Category/
├── GraphQL/                # HotChocolate types, queries, mutations, DataLoaders
│   ├── Types/
│   ├── Queries/
│   ├── Mutations/
│   └── DataLoaders/
├── Persistence/            # MongoDbContext, ProductCatalogDbContext, migrations
├── Configurations/         # EF Core IEntityTypeConfiguration classes
├── Configuration/          # IOptions<T> setup ←⚠️ naming collision with Configurations/
├── Interfaces/             # Repository interfaces (IProductRepository, etc.)
├── Repositories/
├── Services/
├── Handlers/               # Cross-module event handlers (e.g., tenant cascade delete)
├── StoredProcedures/
├── ValueObjects/           # Price.cs
├── Common/                 # Errors/, Events/
├── Logging/
├── ProductCatalogModule.cs
└── ProductCatalogDbMarker.cs
```

## WHERE TO LOOK
| Need | Location |
|------|----------|
| Add product endpoint | `Features/Product/Commands/` or `Queries/` |
| Add GraphQL type | `GraphQL/Types/` |
| Add GraphQL query/mutation | `GraphQL/Queries/` or `GraphQL/Mutations/` |
| Add DataLoader | `GraphQL/DataLoaders/` |
| Add EF entity | `Entities/` + `Configurations/` for mapping |
| Add MongoDB document type | `Entities/` (inherits ProductData) |
| Add stored procedure | `StoredProcedures/` |
| Add price logic | `ValueObjects/Price.cs` |

## CONVENTIONS
- **Entities live in `Entities/` at root**, not `Domain/`. The `Domain/` folder only contains services.
- **`Configurations/`** = EF Core type configs. **`Configuration/`** = IOptions setup. These are different.
- GraphQL DataLoaders solve N+1: `ProductReviewsByProductDataLoader` batches review fetches.
- MongoDB uses BSON discriminators: `ImageProductData` (`_t: "image"`) and `VideoProductData` (`_t: "video"`).
- Module has direct project reference to Reviews — the only cross-module reference in the solution.

## ANTI-PATTERNS
- NEVER put new entities in `Domain/` — they go in `Entities/`.
- NEVER mix EF configs into `Configuration/` — that's for IOptions only. Use `Configurations/`.
- NEVER add a second direct module reference — ProductCatalog→Reviews is the sole exception.
- NEVER query MongoDB from EF Core context — use `MongoDbContext` for `product_data` collection.

## NOTES
- `ProductCatalogDbMarker.cs` — empty marker for EF Core design-time factory.
- MongoDB migrations auto-run at startup via `Kot.MongoDB.Migrations` in `Migrations/` folder (module root, not under Persistence/).
- GraphQL max execution depth: 5. Max page size: 100. Default page size: 20.
