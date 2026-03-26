# WolverineFx Message Bus Architecture

The project uses [WolverineFx](https://wolverine.netlify.app/) as an in-process message bus/mediator. Wolverine is configured with **balanced durability mode** (`DurabilityMode.Balanced`) and convention-based handler discovery. This repository currently uses Wolverine for in-process dispatch only; no external transport is configured here.

---

## Core Concepts

### Messages

Commands, queries, and events are all **plain C# records** with no marker interfaces:

```csharp
// Command returning a result
public sealed record CreateProductsCommand(CreateProductsRequest Request);

// Query
public sealed record GetProductsQuery(ProductFilter Filter);

// Void command
public sealed record DeleteProductReviewCommand(Guid Id);

// Event / notification
public sealed record CacheInvalidationNotification(string CacheTag);
```

Wolverine does not require `ICommand`, `IRequest`, or any marker interface. The message type _is_ the contract.

### Handlers

Handlers are `sealed class` types with a `static HandleAsync` method. The first parameter is the message; all remaining parameters are resolved from DI (**method injection**):

```csharp
public sealed class GetProductsQueryHandler
{
    public static async Task<ProductsResponse> HandleAsync(
        GetProductsQuery request,
        IProductRepository repository,
        CancellationToken ct)
    {
        // ...
    }
}
```

- **No constructor injection** -- dependencies are injected directly into the method.
- **No interfaces to implement** -- Wolverine discovers handlers by convention.
- Return `Task<T>` for queries/commands that produce a result, `Task` for void commands.

### IMessageBus

`IMessageBus` is the single dispatch surface injected into controllers and resolvers:

| Method | Purpose |
|---|---|
| `bus.InvokeAsync<T>(message, ct)` | Send a message and await its `T` result |
| `bus.InvokeAsync(message, ct)` | Send a message with no return value |
| `bus.PublishAsync(message)` | Publish a notification to all registered handlers |

---

## Handler Discovery

Wolverine finds handlers automatically based on naming conventions:

1. Class name ends with `Handler` (e.g., `CreateProductsCommandHandler`).
2. Method is named `HandleAsync` (or `Handle`).
3. First parameter type is the message type.

Assembly scanning is configured in `Program.cs`:

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(CreateProductsCommand).Assembly); // Application
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);              // Api
    opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
    opts.Durability.Mode = DurabilityMode.Balanced;
});
```

No manual handler registration is needed.

---

## Feature Structure

Each feature follows one-handler-per-file with the message record and handler class co-located:

```
Features/{Feature}/
    Commands/
        Create{Feature}Command.cs       -- record + handler class
        Update{Feature}Command.cs
        Delete{Feature}Command.cs
    Queries/
        Get{Feature}ByIdQuery.cs
        Get{Feature}sQuery.cs
    {Feature}ValidationHelper.cs         -- shared validation methods
    Specifications/
    Repositories/
    Mappings/
    DTOs/
    Validation/
```

---

## Adding a New Command

1. Create `Features/{Feature}/Commands/DoSomethingCommand.cs`:

```csharp
public sealed record DoSomethingCommand(DoSomethingRequest Request);

public sealed class DoSomethingCommandHandler
{
    public static async Task<SomethingResponse> HandleAsync(
        DoSomethingCommand command,
        ISomethingRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        CancellationToken ct)
    {
        // business logic
        var entity = new SomeEntity { /* ... */ };
        await repository.AddAsync(entity, ct);
        await unitOfWork.CommitAsync(ct);

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Something));
        return new SomethingResponse(/* ... */);
    }
}
```

2. Add a `FluentValidation` validator for the request DTO if needed and register it in DI.

3. Dispatch from controller:

```csharp
[HttpPost]
public async Task<ActionResult<SomethingResponse>> Create(
    DoSomethingRequest request, CancellationToken ct)
{
    var result = await bus.InvokeAsync<SomethingResponse>(
        new DoSomethingCommand(request), ct);
    return Ok(result);
}
```

That is it -- no DI registration, no interface implementation.

---

## Adding a New Query

```csharp
public sealed record GetSomethingQuery(SomeFilter Filter);

public sealed class GetSomethingQueryHandler
{
    public static async Task<SomethingResponse> HandleAsync(
        GetSomethingQuery request,
        ISomethingRepository repository,
        CancellationToken ct)
    {
        return await repository.GetFilteredAsync(request.Filter, ct);
    }
}
```

Dispatch: `await bus.InvokeAsync<SomethingResponse>(new GetSomethingQuery(filter), ct)`

---

## Adding a New Event

### 1. Define the event record

```csharp
// No marker interface needed
public sealed record UserRegisteredNotification(Guid UserId, string Email, string Username);
```

### 2. Create a handler

```csharp
public sealed class UserRegisteredEmailHandler
{
    public static async Task HandleAsync(
        UserRegisteredNotification @event,
        IEmailTemplateRenderer templateRenderer,
        IEmailQueue emailQueue,
        IOptions<EmailOptions> options,
        CancellationToken ct)
    {
        var html = await templateRenderer.RenderAsync(
            EmailTemplateNames.UserRegistration,
            new { @event.Username, @event.Email, LoginUrl = $"{options.Value.BaseUrl}/login" },
            ct);

        await emailQueue.EnqueueAsync(
            new EmailMessage(@event.Email, "Welcome!", html, EmailTemplateNames.UserRegistration),
            ct);
    }
}
```

### 3. Publish from a command handler

```csharp
await bus.PublishAsync(new UserRegisteredNotification(user.Id, user.Email, user.Username));
```

Multiple handlers can subscribe to the same event -- Wolverine invokes all of them.

---

## Validation

Validation operates at two levels:

### Wolverine FluentValidation Middleware

`opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration)` registers a Wolverine middleware that runs any explicitly registered `IValidator<TMessage>` **before** the handler executes. If validation fails, a `ValidationException` is thrown and the handler is never called.

This covers all messages dispatched through `IMessageBus`.

### FluentValidationActionFilter (REST layer)

A global `IAsyncActionFilter` validates controller action arguments against registered `IValidator<T>` instances. If validation fails, it short-circuits with HTTP 400 and a `ValidationProblemDetails` body before the action method runs.

This covers request DTOs bound from `[FromQuery]` / `[FromBody]` that are not wrapped in a command record.

### Batch Validation

Batch operations use `BatchFailureContext<T>` + `IBatchRule<T>` to collect per-item failures without throwing. Rules are applied sequentially and failures accumulate:

```csharp
var context = new BatchFailureContext<CreateProductRequest>(items);

await context.ApplyRulesAsync(ct,
    new FluentValidationBatchRule<CreateProductRequest>(itemValidator));

// Additional reference checks...
context.AddFailures(BatchFailureMerge.MergeByIndex(categoryFailures, productDataFailures));

if (context.HasFailures)
    return context.ToFailureResponse();
```

Batch rules live in `Application/Common/Batch/Rules/` and include `FluentValidationBatchRule`, `MarkMissingByIdBatchRule`, and `MarkMissingIdsBatchRule`.

---

## DI / Setup

Wolverine is configured entirely in `Program.cs` via `UseWolverine()`. There is no `AddCqrsHandlers()` or Scrutor scanning -- Wolverine handles all handler registration internally.

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(CreateProductsCommand).Assembly);
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
    opts.Durability.Mode = DurabilityMode.Balanced;
});
```

- **Application assembly** -- contains all command/query handlers and event definitions.
- **Api assembly** -- contains infrastructure handlers (e.g., `CacheInvalidationHandler`).
- FluentValidation validators are registered separately in `AddApplicationServices()`.

---

## Controller Dispatch

REST controllers inject `IMessageBus` via primary constructor and dispatch messages in action methods:

```csharp
public sealed class ProductsController(IMessageBus bus) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProductsResponse>> GetAll(
        [FromQuery] ProductFilter filter, CancellationToken ct)
    {
        var products = await bus.InvokeAsync<ProductsResponse>(
            new GetProductsQuery(filter), ct);
        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<BatchResponse>> Create(
        CreateProductsRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<BatchResponse>(
            new CreateProductsCommand(request), ct);
        return OkOrUnprocessable(result);
    }
}
```

---

## GraphQL Dispatch

Hot Chocolate resolvers inject `IMessageBus` via the `[Service]` attribute:

```csharp
public class ProductQueries
{
    public async Task<ProductPageResult> GetProducts(
        ProductQueryInput? input,
        [Service] IMessageBus bus,
        CancellationToken ct)
    {
        var filter = new ProductFilter(/* map from input */);
        var page = await bus.InvokeAsync<ProductsResponse>(
            new GetProductsQuery(filter), ct);
        return new ProductPageResult(page.Page, page.Facets);
    }
}
```

Both REST and GraphQL use the same `IMessageBus` dispatch -- handlers are shared.

---

## Cache Invalidation

Cache invalidation uses a simple notification record and a dedicated handler in the Api layer:

```csharp
// Application layer -- event record
public sealed record CacheInvalidationNotification(string CacheTag);

// Api layer -- handler
public sealed class CacheInvalidationHandler
{
    public static Task HandleAsync(
        CacheInvalidationNotification @event,
        IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct)
        => outputCacheInvalidationService.EvictAsync(@event.CacheTag, ct);
}
```

Command handlers publish after successful writes using `CacheTags` constants:

```csharp
await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Products));
```

`CacheTags` (`Application/Common/Events/CacheTags.cs`) centralizes all tag constants: `Products`, `Categories`, `Reviews`, `Users`, etc.

---

## Fire-and-Forget Events

`PublishSafeAsync` is an extension method on `IMessageBus` that swallows non-cancellation exceptions and logs them as warnings. Use it for notification events whose failure must not break the main command flow:

```csharp
public static async Task PublishSafeAsync<TEvent>(
    this IMessageBus bus, TEvent @event, ILogger logger)
{
    try
    {
        await bus.PublishAsync(@event);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogWarning(ex, "Failed to publish {EventType}.", typeof(TEvent).Name);
    }
}
```

Usage in a handler:

```csharp
await bus.PublishSafeAsync(
    new UserRegisteredNotification(user.Id, user.Email, user.Username), logger);
```

---

## Batch Operations

Batch commands (create/update/delete multiple entities) use a domain-level validation pipeline built on:

- **`BatchFailureContext<TItem>`** -- accumulates per-index failures across multiple validation rules.
- **`IBatchRule<TItem>`** -- interface for a single batch validation step (`FluentValidationBatchRule`, `MarkMissingByIdBatchRule`, `MarkMissingIdsBatchRule`).
- **`BatchFailureMerge`** -- merges failures from independent checks (e.g., category + product-data reference checks) into a single failure list by index.
- **`BatchResponse`** -- returned to the caller with success count + per-item failure details.

The batch validation pipeline runs _inside the handler_, not as middleware, because each batch operation has unique reference-check logic.

All batch infrastructure lives in `Application/Common/Batch/`.

---

## Layer Boundaries

| Component | Layer | Why |
|---|---|---|
| Message records (commands, queries, events) | Application | Domain contracts |
| Command/Query handlers | Application | Business logic |
| `BatchFailureContext`, `IBatchRule` | Application | Batch validation domain logic |
| `IMessageBus` | Application (usage) | Wolverine abstraction, injected where needed |
| `CacheInvalidationHandler` | Api | Depends on `IOutputCacheInvalidationService` |
| `UserRegisteredEmailHandler` | Application | Orchestrates email infrastructure |
| Controllers, GraphQL resolvers | Api | Presentation -- dispatch via `IMessageBus` |
| `FluentValidationActionFilter` | Api | REST-layer validation filter |
| Wolverine configuration (`UseWolverine`) | Api (`Program.cs`) | Infrastructure wiring |

---

## Key Decisions

- **WolverineFx over MediatR** -- convention-based discovery eliminates boilerplate interfaces; method injection removes constructor noise; built-in FluentValidation middleware replaces manual decorator wiring.
- **Balanced durability mode** -- configured to match `Program.cs` while the current application usage remains in-process dispatch only.
- **No marker interfaces** -- commands, queries, and events are plain records. Wolverine routes by type, not by interface.
- **Convention over configuration** -- handler classes are discovered by naming convention (`*Handler` + `HandleAsync`), not by implementing `IRequestHandler<T>` or registering manually.
- **Static `HandleAsync` with method injection** -- dependencies are parameters, not fields. Handlers have no mutable state and no constructor.
- **One handler per file** -- message record + handler class co-located in the same file, following SRP.
- **`IMessageBus` as the single dispatch surface** -- both REST controllers and GraphQL resolvers use the same dispatch mechanism, keeping handlers agnostic of the presentation layer.
- **`PublishSafeAsync` for non-critical events** -- prevents notification failures (e.g., email) from breaking the main command flow.
- **Batch validation in handlers, not middleware** -- each batch operation has unique reference-check logic that cannot be generalized into middleware.
