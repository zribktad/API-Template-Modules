# Remaining Refactoring Items

Status overview of DRY, KISS, and structural improvements identified by automated analysis. Only **not yet implemented** items are listed here.

---

## DRY #1 — Duplicated Code, Magic Strings, Configuration

### #4: Custom OutputCache Attributes
- **Problem:** `[OutputCache(PolicyName = CachePolicyNames.Products)]` repeated 12+ times across controllers
- **Current state:** `CachePolicyNames` constants exist, so magic strings are eliminated — but the verbose attribute syntax remains
- **Suggested fix:** Custom attributes like `[CachedProducts]`, `[CachedCategories]` wrapping `OutputCache`
- **Impact:** Low — cosmetic improvement, strings are already centralized

### #7: Generic ConfigureExporters Method
- **Problem:** `ConfigureTracingExporters` and `ConfigureMetricExporters` in `ObservabilityServiceCollectionExtensions.cs` are nearly identical loops
- **Current state:** Two separate methods, each iterating OTLP endpoints and conditionally adding console exporter
- **Suggested fix:** Generic `ConfigureExporters<TBuilder>(builder, endpoints, enableConsole)` — but `TracerProviderBuilder` and `MeterProviderBuilder` don't share a common interface, so this may require delegates
- **Impact:** Low — only 2 occurrences, abstraction may be more complex than the duplication (KISS concern)

### #12: "DefaultConnection" in DbContextFactory Files
- **Problem:** Two design-time factory files hardcode `"DefaultConnection"` instead of using `ConfigurationSections.DefaultConnection`
- **Files:** `AppDbContextFactory.cs`, `TickerQSchedulerDbContextFactory.cs`
- **Suggested fix:** Reference `ConfigurationSections.DefaultConnection` consistently
- **Impact:** Low — 2 occurrences

### #13: Duplicated ConfigurationBuilder Setup
- **Problem:** Identical `ConfigurationBuilder` setup in two design-time factory files
- **Files:** `AppDbContextFactory.cs`, `TickerQSchedulerDbContextFactory.cs`
- **Suggested fix:** `DesignTimeConfigurationHelper.Build()` shared method
- **Impact:** Low — 2 occurrences, design-time only code

### #14: Duplicated DbContextFactory Logic
- **Problem:** Two separate factory classes with similar structure
- **Files:** `AppDbContextFactory.cs`, `TickerQSchedulerDbContextFactory.cs`
- **Suggested fix:** `DesignTimeDbContextFactoryBase<TContext>` base class
- **Impact:** Low — design-time only, `AppDbContextFactory` has many null-provider implementations that `TickerQSchedulerDbContextFactory` doesn't need

---

## DRY #2 — Repeated Patterns

### #3: Mapping Boilerplate (Projection + CompiledProjection + ToResponse)
- **Problem:** 5 mapping files repeat the same 3-line scaffolding pattern
- **Files:** `CategoryMappings.cs`, `ProductMappings.cs`, `ProductReviewMappings.cs`, `UserMappings.cs`, `TenantMappings.cs`
- **Suggested fix:** `MappingBase<TEntity, TResponse>` abstract class — descendants define only the `Expression`
- **Impact:** Low-Medium — 3 mechanical lines per file; base class changes extension methods to instance methods

### #5: Filter Validator Includes
- **Problem:** 3+ filter validators manually `Include(new PaginationFilterValidator())`, `Include(new DateRangeFilterValidator<T>())`, `Include(new SortableFilterValidator<T>(sortFields))`
- **Suggested fix:** `FilterValidatorBase<TFilter>` base class with automatic Include for Pagination, DateRange, Sortable
- **Impact:** Low — different validators use different combinations of includes; not all include all three

### #7: TelemetryHelper for TagList Construction
- **Problem:** Manual `new TagList { ... }` construction repeated across telemetry files
- **Files:** `CacheTelemetry.cs`, `AuthTelemetry.cs`, `ValidationTelemetry.cs`
- **Suggested fix:** `TelemetryHelper.Record(counter, tags)` with fluent builder
- **Impact:** Low — each telemetry class records different metrics with different tags; abstraction may obscure intent

### #8: Default Logging in QueueConsumerBackgroundService
- **Problem:** Base `HandleErrorAsync` returns `Task.CompletedTask` without logging — derived services duplicate their own logging
- **Files:** `EmailSendingBackgroundService.cs`, `OutgoingWebhookBackgroundService.cs`
- **Suggested fix:** Add default `_logger.LogError(...)` in base `HandleErrorAsync`, let derived classes override only when behavior differs
- **Impact:** Low — 2 occurrences, each with slightly different behavior (email persists failures, webhook only logs)

### #9: Invitation Expiry Check
- **Problem:** `if (invitation.ExpiresAtUtc < now) throw new ConflictException(...)` repeated 2x in `TenantInvitationRequestHandlers`
- **Suggested fix:** Domain method `invitation.ThrowIfExpired(now)` on the entity
- **Impact:** Low — only 2 occurrences, error messages are intentionally different ("expired" vs "expired, create a new one")

---

## KISS #2 — Unnecessary Complexity

### #2: AddQueueWithConsumer with 4 Generic Parameters
- **Problem:** `AddQueueWithConsumer<TImpl, TQueue, TReader, TService>` has complex generic constraints for a pattern used only 3 times
- **File:** `InfrastructureServiceCollectionExtensions.cs:49-62`
- **Suggested fix:** Inline the 3 service registrations at each call site
- **Impact:** Low — the method works correctly, just overly generic for 3 uses

### #3: Dynamic Authorization Policy Registration Loop
- **Problem:** Loop iterates all `Permission.All` values, registering a policy for each with identical authentication schemes and `RequireAuthenticatedUser()`
- **File:** `AuthenticationServiceCollectionExtensions.cs:267-280`
- **Suggested fix:** Custom `IAuthorizationPolicyProvider` that generates policies on-demand without explicit registration
- **Impact:** Medium — eliminates N policy registrations at startup, improves scalability when permissions grow

### #4: Lazy\<T\> + IConfigureOptions for Dragonfly Connection
- **Problem:** Complex lazy initialization pattern combined with `IConfigureOptions` callback pattern
- **File:** `ApiServiceCollectionExtensions.cs:177-204`
- **Suggested fix:** Direct `ConnectionMultiplexer.Connect` in factory delegate, `services.Configure<>()` instead of `ConfigureOptions` wrapper
- **Impact:** Low — works correctly, just unnecessarily complex

### #5: Reflection in ApplyGlobalFilters (MakeGenericMethod)
- **Problem:** Runtime reflection using `MakeGenericMethod` to apply EF Core query filters
- **File:** `AppDbContext.cs:133-161`
- **Suggested fix:** Explicit `IEntityTypeConfiguration<T>` per entity, or `IModelCustomizer` — avoids runtime reflection
- **Impact:** Medium — eliminates reflection but requires explicit configuration per entity type

### #6: Reflection in ValidationBehavior (Nested Object Traversal)
- **Problem:** Complex nested validation with reflection caching — `ReadablePublicInstancePropertiesCache`, `ValidatorsEnumerableTypeCache`, runtime `MakeGenericType`
- **File:** `ValidationBehavior.cs:196-246`
- **Suggested fix:** Validate only top-level request; handle nested validation via FluentValidation's `SetValidator()` explicitly in each validator
- **Impact:** Medium-High — simplifies 50+ lines of reflection code, makes validation behavior predictable

---

## Structure

### #4: Missing Specifications/ Folders
- **Problem:** 3 features lack `Specifications/` folder: `ProductData`, `Bff`, `Examples`
- **Assessment:** These features don't have filterable list queries, so missing folders are **acceptable** — no action needed unless specs are added later

### #5: Organize Filters/ Directory into Subdirectories
- **Problem:** `src/APITemplate.Api/Api/Filters/` contains 6 files mixing unrelated concerns in a flat structure:
  - Validation: `FluentValidationActionFilter.cs`
  - Idempotency: `IdempotencyActionFilter.cs`, `IdempotencyConstants.cs`, `IdempotentAttribute.cs`
  - Webhooks: `ValidateWebhookSignatureAttribute.cs`, `WebhookSignatureResourceFilter.cs`
- **Suggested fix:** Split into subdirectories by concern:
  ```
  Filters/
  ├── Idempotency/
  │   ├── IdempotencyActionFilter.cs
  │   ├── IdempotencyConstants.cs
  │   └── IdempotentAttribute.cs
  ├── Validation/
  │   └── FluentValidationActionFilter.cs
  └── Webhooks/
      ├── ValidateWebhookSignatureAttribute.cs
      └── WebhookSignatureResourceFilter.cs
  ```
- **Impact:** Low — organizational improvement, update namespaces accordingly
