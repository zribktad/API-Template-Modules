# ProductCatalog ↔ Reviews: loosening the dependency

## Current state

- The [`ProductCatalog`](../../src/Modules/ProductCatalog/ProductCatalog.csproj) project has a `ProjectReference` to [`Reviews`](../../src/Modules/Reviews/Reviews.csproj).
- Typical reasons: GraphQL types / dataloaders for reviews, output-cache invalidation (`CacheTags.Reviews`, `CacheTags.Categories` in catalog commands), or shared events.

## Risks of a direct dependency

- A change in Reviews can force a rebuild and full regression of the catalog module.
- Harder to omit Reviews from a deployment or swap in another bounded context.

## Recommended directions (by complexity)

1. **Shared contracts (thin)**  
   Move stable types needed for a read model or cache tags into `SharedKernel.Contracts` (or a dedicated `*.Contracts` project) that references only the kernel—not the whole Reviews module.

2. **Anti-corruption layer in ProductCatalog**  
   ProductCatalog defines its own interface / DTO for “product reviews”; a single infrastructure implementation calls into Reviews (or a database view)—the catalog domain stays independent of the Reviews assembly.

3. **Integration events**  
   Reviews publishes events (e.g. review created); ProductCatalog reacts asynchronously and updates its own projections / cache—appropriate if you already have strong messaging between contexts.

4. **Single aggregate / context**  
   If products and reviews are always consistent together and the team does not want to split them, the dependency can stay—document it as an intentional monolith within one aggregate or use case.

## Recommendation for this repository

Start with an inventory: find every `using Reviews` in ProductCatalog and classify it as (a) read-model / GraphQL only, (b) cache invalidation, or (c) domain logic. Then pick option 1 or 2 as the first step with the lowest risk.
