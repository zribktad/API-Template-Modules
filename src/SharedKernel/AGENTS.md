# SharedKernel — AGENTS.md

## OVERVIEW
Reusable building blocks for all modules. Domain primitives, application utilities, infrastructure helpers, and inter-module contracts. NOT a module — no domain logic, no HTTP endpoints, no business rules.

## STRUCTURE
```
SharedKernel/
├── Domain/             # AuditInfo, IAuditableEntity, ISoftDeletable, ITenantEntity, PagedResponse
├── Application/        # ValidationBehavior, ErrorOr, ICurrentUserContext, Batch, Sorting, Resilience
├── Infrastructure/     # BaseRepository, UnitOfWork, Interceptors, Redis, Health, OutputCache, Idempotency
├── Contracts/          # Events, Commands, Api base classes, Filters — inter-module communication types
└── Extensions/         # DI extension helpers
```

## WHERE TO LOOK
| Need | Location |
|------|----------|
| Add domain interface | `Domain/Interfaces/` |
| Add Wolverine validation middleware | `Application/Validation/ValidationBehavior<T>.cs` |
| Add error type | `Application/Errors/` |
| Add user/tenant context | `Application/Context/ICurrentUserContext.cs` |
| Add repository base behavior | `Infrastructure/Repositories/BaseRepository<T>.cs` |
| Add transaction logic | `Infrastructure/UnitOfWork/` |
| Add EF interceptor | `Infrastructure/SoftDelete/` or `Infrastructure/Auditing/` |
| Add cross-module event | `Contracts/Events/` |
| Add cross-module command | `Contracts/Commands/` |
| Add controller base | `Contracts/Api/ApiControllerBase.cs` |

## CONVENTIONS
- Contracts are records, not classes — immutable message types.
- Infrastructure implements interfaces defined in Domain — never the reverse.
- `ValidationBehavior<T>` auto-runs FluentValidation for all Wolverine handlers.
- `BaseRepository<T>` uses `Ardalis.Specification` for queries — never raw LINQ in repos.

## ANTI-PATTERNS
- NEVER put module-specific logic here — this is purely cross-cutting infrastructure.
- NEVER reference any Module project from SharedKernel.
- NEVER add concrete domain entities — only interfaces and value objects.
