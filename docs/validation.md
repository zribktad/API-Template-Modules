# How to Add Input Validation

This document describes **every validation-related approach that exists in this codebase**, what is **actually wired today**, and how to add new rules. It was checked against the current implementation (not aspirational docs).

---

## Summary: validation surfaces in this app


| Surface                                                    | What it does                                                                                                                                                                                                               | Registered / used in repo?                                                                                                                                                                                                                                                               |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A. ASP.NET Core model validation**                       | Validates bound parameters (body, query, route) using **Data Annotations** and `IValidatableObject`; with `[ApiController]`, invalid model state typically yields **400** before the action runs.                          | **Yes** — all API controllers inherit `[ApiControllerBase](../src/SharedKernel/Contracts/Api/ApiControllerBase.cs)` (`[ApiController]`).                                                                                                                                                 |
| **B. Wolverine `ErrorOrValidationMiddleware`**             | Before a handler runs, resolves `**IValidator<TMessage>**` from DI and runs **FluentValidation** on the **Wolverine message** (`TMessage` = argument to `InvokeAsync`). Stops with `ErrorOr` validation errors if invalid. | **Infrastructure yes; typical HTTP handlers: effectively no** — see [Audit: when does (B) run?](#audit-when-does-b-run).                                                                                                                                                                 |
| **C. Batch item validation (`FluentValidationBatchRule`)** | For batch commands, the handler receives `**IBatchRule<TItem>`** which runs `**IValidator<TItem>**` per item (e.g. each `CreateProductRequest`).                                                                           | **Yes** — ProductCatalog registers `[FluentValidationBatchRule<>](../src/SharedKernel/Application/Batch/Rules/FluentValidationBatchRule.cs)` in `[ProductCatalogModule](../src/Modules/ProductCatalog/ProductCatalogModule.cs)`; commands inject `IBatchRule<CreateProductRequest>` etc. |
| **D. Handler / domain rules**                              | Business checks after load; return `**ErrorOr`** (validation, conflict, not found, …).                                                                                                                                     | **Yes** — standard pattern across modules.                                                                                                                                                                                                                                               |
| **E. MVC `AddFluentValidationAutoValidation()`**           | Automatic FV on every MVC model without Wolverine.                                                                                                                                                                         | **No** — not used (see [What this solution does *not* use](#what-this-solution-does-not-use)).                                                                                                                                                                                           |
| **F. Wolverine `UseFluentValidation()`**                   | Alternative FV integration package.                                                                                                                                                                                        | **No** — not registered.                                                                                                                                                                                                                                                                 |


---

## What this solution does *not* use

- `**AddFluentValidationAutoValidation()`** — FluentValidation is **not** plugged into MVC’s automatic model-validation pipeline as a global filter.
- `**UseFluentValidation()`** (Wolverine.FluentValidation) — not registered in `[Program.cs](../src/APITemplate/Api/Program.cs)`.

Pre-handler FV for Wolverine is implemented only via `**ErrorOrValidationMiddleware**` (`[ErrorOrValidationMiddleware.cs](../src/SharedKernel/Application/Middleware/ErrorOrValidationMiddleware.cs)`).

---

## A. ASP.NET Core and Data Annotations (bound models)

**How it works:** For controller actions, the framework binds parameters and runs **object validation** (including `[Required]`, `[MaxLength]`, custom `ValidationAttribute`, and `IValidatableObject` on the bound type). With `[ApiController]`, automatic **400** responses for bad model state are enabled by default unless you change `[ApiBehaviorOptions](https://learn.microsoft.com/en-us/aspnet/core/web-api/?#automatic-http-400-responses)`.

**In this app:** Use Data Annotations on **request DTOs** that appear **directly** as action parameters (e.g. `[FromBody] CreateProductRequest`). Those rules are enforced **without** FluentValidation if the attributes are present.

**Custom attribute:** `[NotEmptyAttribute](../src/SharedKernel/Application/Validation/NotEmptyAttribute.cs)` for strings/collections where plain `[Required]` is insufficient.

**Limits:** This layer does **not** automatically run `**AbstractValidator<TRequest>`** classes from FluentValidation — only attributes / `IValidatableObject` on the model type unless you add MVC-FV integration (not in this repo).

---

## B. Wolverine `ErrorOrValidationMiddleware` and `IValidator<TMessage>`

**How it works:** In `[Program.cs](../src/APITemplate/Api/Program.cs)`, Wolverine adds:

```csharp
options.Policies.AddMiddleware(
    typeof(ErrorOrValidationMiddleware),
    chain => chain.ShouldApplyErrorOrValidation(WolverineModuleDiscovery.ErrorOrValidationAssemblies)
);
```

`[ShouldApplyErrorOrValidation](../src/APITemplate/Api/Extensions/WolverineHandlerChainExtensions.cs)` returns **true** only if **both** hold:

1. `**HasValidatorIn(messageType, assembly)`** — some **concrete** type in that assembly implements `**IValidator<TMessage>`** where `**TMessage` is exactly the handler’s message type** (the type passed to `InvokeAsync`), per `[WolverineTypeExtensions.HasValidatorIn](../src/APITemplate/Api/Extensions/WolverineTypeExtensions.cs)`.
2. The handler method’s return type is `**ErrorOr<…>`** (possibly wrapped in `Task`/`ValueTask`), per `[IsErrorOrReturnType](../src/APITemplate/Api/Extensions/WolverineTypeExtensions.cs)`.

`[ErrorOrValidationMiddleware.BeforeAsync](../src/SharedKernel/Application/Middleware/ErrorOrValidationMiddleware.cs)` then receives `**IValidator<TMessage>?**` from DI. If **null**, validation is skipped and the handler runs.

**Important:** `AbstractValidator<CreateProductRequest>` implements `**IValidator<CreateProductRequest>`**, **not** `IValidator<CreateProductsCommand>`. For `InvokeAsync(new CreateProductsCommand(...))`, DI resolves `**IValidator<CreateProductsCommand>`** — so a **request-only** validator does **not** satisfy the middleware unless you add a **command-level** validator (e.g. `AbstractValidator<CreateProductsCommand>` with `RuleFor(x => x.Items).ForEach(...)` or `SetValidator` on nested DTOs).

### Audit: when does (B) run?

A repository-wide check shows **no** production `AbstractValidator<T>` where `T` is a **command/query message type** (e.g. `CreateUserCommand`, `GetUsersQuery`). Validators are overwhelmingly `**AbstractValidator<TRequest>`**, `**AbstractValidator<TFilter>**`, etc.

Therefore `**HasValidatorIn` is usually false** for real handler message types, and `**ShouldApplyErrorOrValidation` is false** — the middleware **does not attach** to those chains (the policy’s filter fails).

The middleware **is** covered by unit tests using a dedicated `**MiddlewareTestCommand`** and `**AbstractValidator<MiddlewareTestCommand>**` (`[ErrorOrValidationMiddlewareTests.cs](../tests/APITemplate.Tests/Unit/Middleware/ErrorOrValidationMiddlewareTests.cs)`).

**To actually use (B) for an HTTP flow:** add e.g. `sealed class CreateFooCommandValidator : AbstractValidator<CreateFooCommand>` in an assembly listed in `[ErrorOrValidationAssemblies](../src/APITemplate/Api/WolverineModuleDiscovery.cs)`, register validators from that assembly with `**AddValidatorsFromAssemblyContaining`**, and ensure the handler returns `**ErrorOr<T>**`.

### Assemblies in `ErrorOrValidationAssemblies`

`[WolverineModuleDiscovery.cs](../src/APITemplate/Api/WolverineModuleDiscovery.cs)` lists **five** assemblies for the **“has validator”** scan, anchored by types: `CreateUserCommand`, `CreateProductsCommand`, `CreateProductReviewCommand`, `UploadFileCommand`, `SubmitJobCommand` (Identity, ProductCatalog, Reviews, FileStorage, BackgroundJobs). If you add a **new module** with command-level validators, **extend this list** so `HasValidatorIn` can see `IValidator<TMessage>` types in that assembly.

**FluentValidation DI registration (`AddValidatorsFromAssemblyContaining`) — current modules:**

- `[IdentityModule](../src/Modules/Identity/IdentityModule.Directory.cs)` — `AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>` (filters out generic type definitions)
- `[ProductCatalogModule](../src/Modules/ProductCatalog/ProductCatalogModule.cs)` — `AddValidatorsFromAssemblyContaining<ProductCatalogDbMarker>`
- `[ReviewsRuntimeBridge](../src/Modules/Reviews/ReviewsRuntimeBridge.cs)` — `AddValidatorsFromAssemblyContaining<CreateProductReviewRequestValidator>`

**FileStorage / BackgroundJobs** are included in `ErrorOrValidationAssemblies` but **do not** currently call `AddValidatorsFromAssemblyContaining`; there are also **no** `AbstractValidator<T>` types under those module paths in the repo snapshot used for this doc. **Notifications / Webhooks / Chatting** are not in `ErrorOrValidationAssemblies`; add an assembly entry **and** register validators if you introduce FV there and want (B) to apply.

---

## C. Batch: `FluentValidationBatchRule<TItem>` + `IValidator<TItem>`

**How it works:** ProductCatalog registers:

```csharp
services.AddScoped(typeof(IBatchRule<>), typeof(FluentValidationBatchRule<>));
```

`[FluentValidationBatchRule<TItem>](../src/SharedKernel/Application/Batch/Rules/FluentValidationBatchRule.cs)` resolves `**IValidator<TItem>**` and validates **each batch item** inside the handler pipeline (not via `ErrorOrValidationMiddleware`).

**Batch items and duplicate attribute validation:** For HTTP batch endpoints, ASP.NET Core already validates the bound body (including nested items with Data Annotations). `**IValidator<TItem>`** for those items should therefore add only **FluentValidation-only** rules (e.g. cross-field logic), **not** a second pass through `[DataAnnotationsValidator<T>](../src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs)`. In ProductCatalog, `[ProductRequestValidatorBase<T>](../src/Modules/ProductCatalog/Features/Product/Shared/ProductRequestValidatorBase.cs)` extends `**AbstractValidator<T>`** with the shared description/price rule; category batch validators are empty `**AbstractValidator<T>**` hooks when attributes alone suffice.

**Example:** `[CreateProductsCommand](../src/Modules/ProductCatalog/Features/Product/CreateProducts/CreateProductsCommand.cs)` + `[CreateProductRequestValidator](../src/Modules/ProductCatalog/Features/Product/CreateProducts/CreateProductRequestValidator.cs)` (cross-field rules only).

For **why** batch items no longer use a Data Annotations bridge here, see [AttributedModelValidator](#attributedmodelvalidator-shared-data-annotations-semantics) (shared semantics, not duplicate product rules).

---

## D. `DataAnnotationsValidator<T>` (bridge attributes → FluentValidation)

`[DataAnnotationsValidator<T>](../src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs)` subclasses `**AbstractValidator<T>`** and maps `[AttributedModelValidator.Validate](../src/SharedKernel/Application/Validation/AttributedModelValidator.cs)` into FluentValidation failures. Use it when `**IValidator<T>**` must honor **Data Annotations** and you are **not** already validating the same instance via MVC (e.g. non-HTTP entry points, or DTOs not bound as action parameters).

**Avoid** wiring this bridge for **batch item** types that are part of an API body already validated by **(A)** — prefer `**AbstractValidator<TItem>`** with only extra rules.

**Not sufficient alone** for (B) unless `**T`** is also the Wolverine **message** type (see above).

---

## `AttributedModelValidator` (shared Data Annotations semantics)

`[AttributedModelValidator](../src/SharedKernel/Application/Validation/AttributedModelValidator.cs)` is **not** a separate product rule or a second “layer” of business validation. It is a **single shared implementation** of “run Data Annotations on a CLR object the same way this solution expects,” so that:

1. `**DataAnnotationsValidator<T>`** does not duplicate dozens of lines of `Validator.TryValidateObject` plus record-specific logic — it calls `**AttributedModelValidator.Validate(model)**` and maps results into FluentValidation failures.
2. **Unit tests** can assert attribute behavior **without** going through `IValidator<T>` when the production validator intentionally only adds FluentValidation rules (e.g. batch items after the duplicate-attribute fix). Tests use `[DataAnnotationsTestHelper](../tests/APITemplate.Tests/Unit/Helpers/DataAnnotationsTestHelper.cs)`, which delegates to `**AttributedModelValidator`** so expectations match the bridge and primary-constructor `**record**` types (attributes on constructor parameters are not always covered the same way by a naïve `TryValidateObject`-only helper).

**What it runs:**

- `Validator.TryValidateObject` with `**validateAllProperties: true`** (property-level validation on the instance).
- An extra pass for **validation attributes on primary constructor parameters** of `**record`** types (aligned with the previous inline logic in `DataAnnotationsValidator`), so behavior stays consistent with how attributes on record positional parameters are evaluated.

**What it is not:** a new validation “surface” in the summary table — it is **shared infrastructure**. HTTP traffic still relies primarily on **(A)** for attributes on bound DTOs; `**AttributedModelValidator`** matters most where you need the **same semantics** in tests or in `**DataAnnotationsValidator<T>`** without copying the implementation.

**Non-HTTP callers:** If a command is invoked **without** going through MVC (e.g. internal message dispatch), attributes on nested batch items are **not** automatically enforced by **(A)**; that was already true whenever attribute rules were only replayed via FV. If you add such entry points, validate explicitly (handler, command-level validator, or call `**AttributedModelValidator`** / `**DataAnnotationsValidator<T>**` as appropriate).

---

## E. Pure FluentValidation (`AbstractValidator<T>`)

Use `**RuleFor**`, `**When**`, `**Include**`, `**MustAsync**`, etc., for cross-field logic, async checks, or filters.

**Shared pieces:** `[PaginationFilterValidator](../src/SharedKernel/Application/Validation/PaginationFilterValidator.cs)`, `[DateRangeFilterValidator<T>](../src/SharedKernel/Application/Validation/DateRangeFilterValidator.cs)`, `[SortableFilterValidator<T>](../src/SharedKernel/Application/Validation/SortableFilterValidator.cs)`.

**Example — conditional rules on a base:** `[ProductRequestValidatorBase<T>](../src/Modules/ProductCatalog/Features/Product/Shared/ProductRequestValidatorBase.cs)`.

**Async / DI:** Validators registered in DI can take constructor dependencies; use `**MustAsync`** sparingly for I/O (prefer domain/handler for heavy business rules).

---

## F. Service- / handler-level validation (`ErrorOr`)

After binding (and after any batch item validation), handlers return `**ErrorOr**` for business violations — conflicts, not found, forbidden, etc. Mapping to HTTP responses goes through the API `**ErrorOr` → ProblemDetails** pipeline (`[ApiExceptionHandler](../src/APITemplate/Api/ExceptionHandling/ApiExceptionHandler.cs)` and related), **not** through `ErrorOrValidationMiddleware` (that middleware only runs **FluentValidation before** the handler when a matching `**IValidator<TMessage>`** exists).

```csharp
public static async Task<ErrorOr<ProductResponse>> HandleAsync(
    CreateProductCommand command,
    IProductRepository repository,
    CancellationToken ct)
{
    var existing = await repository.GetByNameAsync(command.Request.Name, ct);
    if (existing is not null)
        return Error.Conflict(description: "A product with the same name already exists.");

    // ...
}
```

---

## Rule of thumb (which surface to use)

- **Simple field rules on API input** — Data Annotations on the request DTO + rely on **(A)** where the DTO is bound to the action.
- **Batch create/update items** — `**IValidator<TItem>`** + `**FluentValidationBatchRule<TItem>**` (**(C)**).
- **FluentValidation on the Wolverine message** — implement `**AbstractValidator<TMessage>`** for the **exact** command/query type and satisfy **(B)**’s assembly list + DI registration.
- **Business rules needing DB / aggregates** — **handler/domain** + `**ErrorOr`** (**(F)**).

---

## Step 1 — Data Annotations on the DTO

```csharp
using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

public sealed record CreateProductRequest(
    [NotEmpty(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
    string Name,
    string? Description,
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    decimal Price,
    Guid? CategoryId = null) : IProductRequest;
```

---

## Step 2 — Batch / FV-only rules (no second attribute pass)

```csharp
using FluentValidation;
using ProductCatalog.Features.Product.Shared;

namespace ProductCatalog.Features.Product.CreateProducts;

public sealed class CreateProductRequestValidator
    : ProductRequestValidatorBase<CreateProductRequest>;
```

---

## Step 3 — Cross-field rules on `AbstractValidator<T>` (shared base)

```csharp
using FluentValidation;
using ProductCatalog.Features.Product.Shared;

public abstract class ProductRequestValidatorBase<T> : AbstractValidator<T>
    where T : class, IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
```

Use `[DataAnnotationsValidator<T>](../src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs)` only when you need the attribute bridge **without** duplicate MVC validation on the same instance.

---

## Step 4 — Compose shared validators (`Include`)

See `[ProductFilterValidator](../src/Modules/ProductCatalog/Features/Product/GetProducts/ProductFilterValidator.cs)` pattern in repo; shared building blocks live under `[src/SharedKernel/Application/Validation/](../src/SharedKernel/Application/Validation/)`.

---

## HTTP validation error shape

Failures from **(A)** use **ValidationProblemDetails** / model-state shape. Failures from **(B)** when active use `[ErrorCatalog.General.ValidationFailed](../src/SharedKernel/Application/Errors/ErrorCatalog.cs)` in `ErrorOr` metadata. Exact JSON depends on exception handler and `ErrorOr` mapping configuration.

---

## Testing

- Call `**Validate(...)`** / `**ValidateAsync**` on FluentValidation validators in tests.
- To assert **Data Annotations** behavior (without relying on an `AbstractValidator` that only adds cross-field rules), use `[DataAnnotationsTestHelper](../tests/APITemplate.Tests/Unit/Helpers/DataAnnotationsTestHelper.cs)` — it delegates to `[AttributedModelValidator](../src/SharedKernel/Application/Validation/AttributedModelValidator.cs)` so results match the bridge and `**record`** constructor parameters (see `[AttributedModelValidator` (shared Data Annotations semantics)](#attributedmodelvalidator-shared-data-annotations-semantics)).
- `[ErrorOrValidationMiddleware](../tests/APITemplate.Tests/Unit/Middleware/ErrorOrValidationMiddlewareTests.cs)` tests the middleware in isolation.
- Some validator test files are explicitly excluded in `[APITemplate.Tests.csproj](../tests/APITemplate.Tests/APITemplate.Tests.csproj)` (e.g. `CreateProductReviewRequestValidatorTests.cs`); adjust the project file if tests do not run.

---

## Checklist for new features

- Put **Data Annotations** on bound DTOs where **(A)** should catch bad input early.
- For **batch** endpoints: wire `**IBatchRule<TItem>`** + `**IValidator<TItem>**` (**(C)**).
- For **Wolverine middleware** (**(B)**): add `**AbstractValidator<TMessage>`** for the **exact** message type, register assembly in DI, add assembly to `**ErrorOrValidationAssemblies`**, handler returns `**ErrorOr<T>**`.
- Register validators: `**AddValidatorsFromAssemblyContaining<...>**` in the module that owns the validators.
- Put **business** rules that need loaded state in the **handler** with `**ErrorOr`** (**(F)**).

---

## Key files (reference)


| File                                                                                                                                                  | Purpose                                                                                                                |
| ----------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `[src/SharedKernel/Contracts/Api/ApiControllerBase.cs](../src/SharedKernel/Contracts/Api/ApiControllerBase.cs)`                                       | `[ApiController]` base for API routes                                                                                  |
| `[src/SharedKernel/Application/Validation/AttributedModelValidator.cs](../src/SharedKernel/Application/Validation/AttributedModelValidator.cs)`       | Shared DA semantics (`TryValidateObject` + record constructor params); used by `DataAnnotationsValidator<T>` and tests |
| `[src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs](../src/SharedKernel/Application/Validation/DataAnnotationsValidator.cs)`       | Data Annotations → FluentValidation bridge                                                                             |
| `[src/SharedKernel/Application/Middleware/ErrorOrValidationMiddleware.cs](../src/SharedKernel/Application/Middleware/ErrorOrValidationMiddleware.cs)` | Wolverine FV before handler                                                                                            |
| `[src/SharedKernel/Application/Batch/Rules/FluentValidationBatchRule.cs](../src/SharedKernel/Application/Batch/Rules/FluentValidationBatchRule.cs)`   | Per-item FV in batch handlers                                                                                          |
| `[src/APITemplate/Api/Program.cs](../src/APITemplate/Api/Program.cs)`                                                                                 | Wolverine + middleware policy                                                                                          |
| `[src/APITemplate/Api/WolverineModuleDiscovery.cs](../src/APITemplate/Api/WolverineModuleDiscovery.cs)`                                               | Handler assemblies + `ErrorOrValidationAssemblies`                                                                     |
| `[src/APITemplate/Api/Extensions/WolverineHandlerChainExtensions.cs](../src/APITemplate/Api/Extensions/WolverineHandlerChainExtensions.cs)`           | `ShouldApplyErrorOrValidation`                                                                                         |
| `[src/APITemplate/Api/Extensions/WolverineTypeExtensions.cs](../src/APITemplate/Api/Extensions/WolverineTypeExtensions.cs)`                           | `HasValidatorIn`, `IsErrorOrReturnType`                                                                                |
| `[src/APITemplate/Api/ExceptionHandling/ApiExceptionHandler.cs](../src/APITemplate/Api/ExceptionHandling/ApiExceptionHandler.cs)`                     | Global errors / ProblemDetails                                                                                         |


