# Validation

This document describes every validation mechanism in this codebase, how they wire together, and the full request lifecycle from HTTP arrival to response.

---

## Request lifecycle — where validation fits

```
HTTP Request
  │
  ▼
[1] ASP.NET Core model binding
      Binds [FromBody] / [FromQuery] to request/filter DTO.
      Runs Data Annotations on the bound DTO (property-level + constructor parameters for records).
      → Invalid: 400 ProblemDetails before the controller action runs ([ApiController] attribute).
  │
  ▼
[2] Controller action
      Maps DTO → Wolverine Command/Query and dispatches via bus.InvokeAsync(command, ct).
  │
  ▼
[3] Wolverine middleware — DataAnnotationsValidationMiddleware
      Runs IValidator.Validate(message) on the command/query object itself.
      Applied only to handlers whose return type is ErrorOr<T>.
      → Invalid: returns (HandlerContinuation.Stop, ErrorOr<T> errors) — handler never executes.
      NOTE: Commands currently have no DataAnnotation attributes → middleware is a no-op for HTTP flows.
      It activates for non-HTTP entry points (scheduled jobs, outbox retry) where MVC never runs.
  │
  ▼
[4] Handler — BatchFailureContext + IBatchRule<TItem>
      For batch commands only. Runs DataAnnotationsBatchRule<TItem> per item.
      Collects per-item failures without throwing.
      → Any failures: returns BatchResponse with per-index error details.
  │
  ▼
[5] Handler — domain / business rules (ErrorOr)
      DB-dependent checks: entity existence, uniqueness, business constraints.
      Returns ErrorOr<T> with Error.Conflict / Error.NotFound / etc.
  │
  ▼
[6] Response mapping
      ErrorOr<T> → RFC 7807 ProblemDetails via ApiExceptionHandler.
      HTTP 400 for validation, 404 for not found, 409 for conflict, etc.
```

---

## Validation surfaces

| # | Surface | Validates | When active |
|---|---------|-----------|-------------|
| **A** | ASP.NET Core model validation | Request/filter DTOs (Data Annotations) | Every MVC `[ApiController]` endpoint |
| **A'** | Wolverine HTTP validation policy | Request/filter DTOs (Data Annotations) | Every Wolverine HTTP endpoint (`MapWolverineEndpoints` + `UseDataAnnotationsValidationProblemDetailMiddleware`) |
| **B** | `DataAnnotationsValidationMiddleware` | Wolverine command/query objects | Handlers returning `ErrorOr<T>`; currently relevant for non-HTTP entry points |
| **C** | `DataAnnotationsBatchRule<TItem>` via `IBatchRule<T>` | Batch item DTOs | Batch command handlers |
| **D** | Handler / domain rules | Business invariants needing loaded state | Inside every handler that needs it |

---

## A. ASP.NET Core model validation (Data Annotations on DTOs)

Every controller inherits `ApiControllerBase` which applies `[ApiController]`. This enables automatic 400 responses before the action runs when model state is invalid.

**What gets validated:** any class/record declared as a direct action parameter (`[FromBody]`, `[FromQuery]`, `[FromRoute]`).

**Validation runs:**
- `Validator.TryValidateObject` with `validateAllProperties: true` — covers properties.
- For `record` types: additionally runs attributes declared on primary constructor parameters (via `DataAnnotationsValidator`; standard MVC may miss these for records).

**Custom attributes available in `SharedKernel.Application.Validation`:**

| Attribute | Purpose |
|-----------|---------|
| `[NotEmpty]` | Rejects null, empty string, empty collection |
| `[CaseInsensitiveAllowedValues("a","b")]` | Allowed string values, case-insensitive; null always passes |
| `[SortDirection]` | Accepts `"asc"`, `"desc"`, null |
| `[NoEmptyGuidItems]` | Rejects `Guid.Empty` inside a collection |
| `[NoWhitespaceItems]` | Rejects whitespace-only strings inside a collection |
| `[MaxLengthItems(n)]` | Max length per string item in a collection |
| `[GreaterThanOrEqualToProperty("OtherProp")]` | Cross-field: value ≥ other property |
| `[RequiredWhenDecimalPropertyExceeds("Prop", threshold)]` | Required string when another decimal exceeds threshold |

Standard .NET attributes (`[Required]`, `[Range]`, `[MaxLength]`, `[MinLength]`) work as normal.

**Error response shape:**

`ApiBehaviorOptions.InvalidModelStateResponseFactory` maps model-state errors to `Error.Validation(ErrorCatalog.General.ValidationFailed, ...)` and returns RFC 7807 ProblemDetails.

### Record DTO convention (MVC vs. Wolverine HTTP)

MVC (`[ApiController]`) reads validation attributes from **both** record primary-constructor parameters and from generated properties. Wolverine HTTP (`DataAnnotationsHttpValidationExecutor` → `Validator.TryValidateObject`) reads them **only from properties**. Pick the DTO style per endpoint host:

| DTO role | Binding style | Recommended record shape |
|----------|---------------|---------------------------|
| Request **body** (`[FromBody]`) — any host | JSON deserialization (System.Text.Json) | `public sealed record Foo { [Range] public int X { get; init; } }` — attrs naturally on properties, `init` is honored by the JSON binder |
| Query **filter** (`[FromQuery]`) — MVC | MVC model binder | Either primary-ctor `record Foo([Range] int X = 0)` **or** `{ get; init; }` — both work |
| Query **filter** (`[FromQuery]`) — Wolverine HTTP | `QueryStringBindingFrame` | **Primary-ctor only**, with `[property: ...]` targets: `record Foo([property: Range] int X = 0)`. Wolverine codegen assigns `filter.X = value;` after `new`, which `init`-only setters reject at compile time — so class-style records break query binding. The `[property:]` target moves the attribute onto the generated property so the validation policy wires it up. |

> **Known limitation — pagination on inherited filters:** `PaginationFilter` declares `[Range]` on its ctor parameters (no `[property:]` target) because moving them onto the generated properties breaks MVC validation for derived filters, and a derived primary-ctor param that shadows a base property cannot itself carry a `[property:]` target (CS0657). A Wolverine HTTP endpoint binding a `PaginationFilter`-derived filter therefore lets invalid `PageNumber`/`PageSize` values through the `DataAnnotations` policy. Composing pagination as a nested `PaginationFilter` ctor param also fails — Wolverine's query-string binder only handles primitive/string/nullable-primitive types and cannot flatten a complex type from a flat query string. When filter DTOs eventually migrate to a unified class-style record + custom `IHttpPolicy` this section goes away.

> **Current workaround:** Wolverine filters that need `PageNumber`/`PageSize` validation declare those fields inline on their own primary ctor with `[property: Range]` instead of inheriting `PaginationFilter`. MVC-bound filters keep inheriting `PaginationFilter` unchanged. See `ProductReviewFilter` as the reference.

#### Why every inheritance/composition variant fails on Wolverine

Ruled-out options (each validated empirically before landing on the inline fix):

| Attempt | Why it fails |
|---------|--------------|
| Inherit `PaginationFilter`; re-declare `PageNumber`/`PageSize` in derived primary ctor with `[property: Range]` | **CS0657** — a shadowed ctor param cannot carry a `[property:]` target |
| Inherit `PaginationFilter`; move its `[Range]` attributes to `[property: Range]` | Breaks MVC validation for every other derived filter (ctor-param shadowing vs. attributed base property) — existing 400-for-invalid-query tests start returning 500 |
| Inherit `PaginationFilter`; duplicate attributes as both `[param: Range]` and `[property: Range]` on base ctor params | Same MVC breakage as above |
| Inherit `PaginationFilter`; derived primary ctor omits `PageNumber`/`PageSize` | Wolverine `QueryStringBindingFrame` iterates only the derived ctor params, so inherited base properties are never bound from the query string — `?pageNumber=2` is silently ignored |
| Compose via nested `PaginationFilter Pagination` ctor param with a dedicated `[PaginationFilterValidator(...)]` attribute | Wolverine's `TryFindOrCreateQuerystringValue` handles only `string`, `string[]`, and nullable primitives; a complex-typed ctor param produces a null `HttpElementVariable`, then codegen NREs on `x.Usage`. Validation would work, but binding doesn't |

Escape hatches that *would* restore inheritance/composition but were deferred as YAGNI at one Wolverine filter:

- Write a custom `IHttpPolicy` that replaces `QueryStringBindingFrame` to either flatten nested records from flat query strings, or to bind inherited `init` properties alongside ctor params.
- Migrate all filter DTOs (and `PaginationFilter`) to class-style records with `{ get; init; }` properties plus the custom policy above (Wolverine's default codegen assigns `filter.X = value;` after `new`, which `init` rejects).

---

## B. Wolverine middleware — DataAnnotationsValidationMiddleware

Declared in `src/SharedKernel/Application/Middleware/DataAnnotationsValidationMiddleware.cs`.

Registered in `Program.cs`:
```csharp
options.Policies.AddMiddleware(
    typeof(DataAnnotationsValidationMiddleware),
    chain => chain.ShouldApplyDataAnnotationsValidation()
);
```

`ShouldApplyDataAnnotationsValidation()` returns `true` when the handler's return type is `ErrorOr<T>` (possibly wrapped in `Task`/`ValueTask`).

**What it does:** calls `IValidator.Validate(message)` on the Wolverine message. If there are failures it returns `(HandlerContinuation.Stop, errors)` and the handler never runs.

**Current state:** No command or query in the codebase has DataAnnotation attributes directly on its own parameters. The middleware therefore does nothing during normal HTTP flows (MVC already validated the DTO, and the Command is just a wrapper with no annotations).

**Where it matters:**
- Scheduled jobs (TickerQ) that dispatch directly to a handler without HTTP.
- Outbox worker retrying a message — MVC never ran for this invocation.
- Any future command that gains its own validation attributes.

---

## C. Batch validation — DataAnnotationsBatchRule<TItem>

For batch commands (`CreateProductsCommand`, `UpdateProductsCommand`, etc.) items are validated inside the handler using `IBatchRule<TItem>`.

`DataAnnotationsBatchRule<TItem>` is registered once in the composition root (`AddApiFoundation`):
```csharp
services.AddScoped(typeof(IBatchRule<>), typeof(DataAnnotationsBatchRule<>));
```

`DataAnnotationsBatchRule<TItem>` calls `IValidator.Validate(item)` for every item in the batch and accumulates failures by index in a `BatchFailureContext<TItem>`.

**Handler pattern:**
```csharp
public static async Task<ErrorOr<BatchResponse>> HandleAsync(
    CreateProductsCommand command,
    IBatchRule<CreateProductRequest> itemRule,
    CancellationToken ct)
{
    BatchFailureContext<CreateProductRequest> ctx = new(command.Request.Items);
    await ctx.ApplyRulesAsync(ct, itemRule);   // runs DataAnnotations per item

    if (ctx.HasFailures)
        return ctx.ToFailureResponse();

    // ... persist
}
```

**Note on duplication:** For HTTP batch endpoints MVC already validated the outer request wrapper (`[FromBody] UpdateProductsRequest`). The batch rule re-validates each *item* inside using `IValidator` — this is the same attribute set but applied per-item, not to the outer wrapper. There is no redundant double-validation of the same object.

---

## D. Handler / domain rules (ErrorOr)

Business checks that require loaded state (entity existence, uniqueness, permissions) live inside the handler and return `ErrorOr<T>`:

```csharp
public static async Task<ErrorOr<ProductResponse>> HandleAsync(
    CreateProductsCommand command,
    IProductRepository repository,
    CancellationToken ct)
{
    if (await repository.ExistsByNameAsync(command.Name, ct))
        return Error.Conflict(description: "A product with this name already exists.");

    // ...
}
```

These errors flow through the same `ErrorOr` → ProblemDetails pipeline as validation errors.

---

## DataAnnotationsValidator (IValidator) — shared implementation

`src/SharedKernel/Application/Validation/IValidator.cs`
`src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs`

Single shared implementation of "run Data Annotations on a CLR object," exposed via the
`IValidator` abstraction and registered as a singleton in `AddApiFoundation`. Used by:
- **Surface A** — MVC uses it internally for property-level validation.
- **Surface B** — `DataAnnotationsValidationMiddleware` receives `IValidator` as a Wolverine-resolved parameter and calls it on Wolverine messages.
- **Surface C** — `DataAnnotationsBatchRule<TItem>` constructor-injects `IValidator` and calls it per batch item.
- **Unit tests** — `DataAnnotationsTestHelper` holds a private `DataAnnotationsValidator` instance so tests match production behavior exactly.

**What it runs:**
1. `Validator.TryValidateObject` with `validateAllProperties: true` — property-level attributes.
2. An additional pass over primary constructor parameters for `record` types — ensures attributes declared on positional parameters are evaluated (standard `TryValidateObject` may not cover them).
3. One-level recursion into nested complex public properties (e.g. command wrappers like `CreateFooCommand(FooRequest Request)`).

**Performance:** Reflection lookups (`GetConstructors`, `GetParameters`, `GetCustomAttributes`, `GetProperty`) are cached in a per-instance `ConcurrentDictionary` keyed by `Type`, so the cost is paid once per type for the lifetime of the singleton.

---

## .NET 10 `Microsoft.Extensions.Validation` — migration status

The project already registers `services.AddValidation()` in `ApiServiceCollectionExtensions.cs`.
One pilot DTO (`CreateUserRequest`) is annotated with `[ValidatableType]` (experimental,
`ASP0029` suppressed locally) as a probe to check whether the .NET 10 source-generator
validation pipeline fires for **Wolverine HTTP** endpoints.

**Current status:** pilot marker applied; empirical runtime verdict pending (requires live
API + invalid request to observe whether validation triggers via endpoint filter before
Wolverine middleware runs).

**Full migration (S3) deferred** until:
- `[ValidatableType]` leaves experimental status (expected .NET 11 stable).
- Wolverine ships native integration with `AddValidation()` (v5.29.0 does not have it).

The `IValidator` abstraction is the migration seam — swapping implementations (e.g. to a
source-generator-backed validator) will not require changes at any of the call sites.

---

## What this codebase does NOT use

| Mechanism | Status | Reason |
|-----------|--------|--------|
| FluentValidation | **Removed** | Replaced by Data Annotations; was used for request/filter validators and Wolverine middleware |
| `WolverineFx.FluentValidation` | **Removed** | Package uninstalled; `DataAnnotationsValidationMiddleware` replaces the old `ErrorOrValidationMiddleware` |
| `FluentValidation.DependencyInjectionExtensions` | **Removed** | No validators to register |
| `AddFluentValidationAutoValidation()` | **Never used** | MVC's built-in Data Annotations pipeline is sufficient |
| `AbstractValidator<T>` | **Removed** | All validators migrated to Data Annotations attributes |

---

## Testing validation

**Data Annotations attributes** — use `DataAnnotationsTestHelper` which delegates to `DataAnnotationsValidator`:

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
public void InvalidName_FailsValidation(string? name)
{
    IReadOnlyList<ValidationResult> results =
        DataAnnotationsTestHelper.Validate(new CreateProductRequest(name!, null, 9.99m));

    results.ShouldContain(r => r.MemberNames.Contains("Name"));
}
```

**Middleware** — tested in isolation in `tests/APITemplate.Tests/Unit/Middleware/`.

**Batch rule** — tested by injecting `DataAnnotationsBatchRule<T>` directly.

**Integration** — `tests/APITemplate.Tests/Integration/Validation/BoundaryValidationIntegrationTests.cs` sends real HTTP requests and asserts 400 responses.

---

## Rule of thumb

| Scenario | Use |
|----------|-----|
| Simple per-field rule on HTTP input DTO | Data Annotation on the DTO; surface **A** handles it |
| Cross-field rule (MaxPrice ≥ MinPrice) | `[GreaterThanOrEqualToProperty]` attribute |
| Conditional required field | `[RequiredWhenDecimalPropertyExceeds]` attribute |
| Allowed string values (sort field) | `[CaseInsensitiveAllowedValues]` attribute |
| Batch item validation | Data Annotations on the item DTO; surface **C** handles it |
| Business rule needing DB/aggregates | Handler + `ErrorOr` — surface **D** |

---

## Key files

| File | Purpose |
|------|---------|
| `src/SharedKernel/Application/Validation/IValidator.cs` | Abstraction for DI / swap-in of alternate implementations |
| `src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs` | Core: runs Data Annotations + record constructor param pass |
| `src/SharedKernel/Application/Middleware/DataAnnotationsValidationMiddleware.cs` | Wolverine pre-handler middleware (surface B) |
| `src/SharedKernel/Application/Batch/Rules/DataAnnotationsBatchRule.cs` | Per-item batch validation (surface C) |
| `src/SharedKernel/Application/Validation/GreaterThanOrEqualToPropertyAttribute.cs` | Cross-field comparison attribute |
| `src/SharedKernel/Application/Validation/RequiredWhenDecimalPropertyExceedsAttribute.cs` | Conditional required attribute |
| `src/SharedKernel/Application/Validation/CaseInsensitiveAllowedValuesAttribute.cs` | Allowed-values attribute (null-safe, case-insensitive) |
| `src/SharedKernel/Application/Validation/NotEmptyAttribute.cs` | Non-empty string/collection attribute |
| `src/SharedKernel/Application/Errors/ErrorCatalog.cs` | Error codes (`GEN-0400` for validation) |
| `src/APITemplate/Api/Extensions/ApiServiceCollectionExtensions.cs` | Registers `IBatchRule<>` → `DataAnnotationsBatchRule<>` |
| `src/APITemplate/Api/Extensions/WolverineHandlerChainExtensions.cs` | `ShouldApplyDataAnnotationsValidation` filter |
| `src/APITemplate/Api/Extensions/WolverineTypeExtensions.cs` | `IsErrorOrReturnType` helper |
| `src/APITemplate/Api/Program.cs` | Wolverine middleware policy registration |
| `tests/APITemplate.Tests/Integration/Validation/BoundaryValidationIntegrationTests.cs` | End-to-end HTTP validation tests |
