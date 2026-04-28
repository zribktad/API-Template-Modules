# Komplexná analýza projektu — API-Template-Monolith

**Dátum**: 2026-04-28
**Branch**: `feature/critical-tests`
**Analytik**: 4 paralelné špecializované agenty (architektúra, kvalita kódu, testovanie/DevEx, security/performance) + integrované zistenia z `ARCHITECTURE_ANALYSIS.md` (Structure / Patterns&SOLID / Data Flow agenty)
**Rozsah**: 864 src C# súborov (41 192 LOC), 189 test súborov (24 877 LOC), 8 modulov + SharedKernel + APITemplate.Api

---

## Executive summary

`API-Template-Monolith` je **nadpriemerne zrelý .NET 10 modulárny monolit** s moderným stackom (WolverineFx, EF Core 10, OpenTelemetry, Keycloak BFF, Testcontainers, xUnit v3). Architektúra rešpektuje CQRS, durable outbox/inbox, multi-tenancy a ErrorOr-based error handling. Stack je koherentný a production-grade.

**Kde je projekt silný**:
- disciplinovaný build (`TreatWarningsAsErrors`, centralized package management, nullable enabled),
- robustný `UnitOfWork<TContext>` so savepointami a execution-strategy retries,
- automatické module-boundary architecture testy (`NetArchTest`),
- source-generated logging, žiadny sync-over-async, žiadne `TODO`/`FIXME`,
- BFF + Keycloak + Wolverine outbox je production-grade.

**Novo flagované High-impact dlhy** (z rozšírenej analýzy, sekcie 8–9):
- Mongo kolekcie ProductData **bez explicitných indexov** (full-scan),
- `OnDelete(DeleteBehavior.SetNull)` Product→Category **nikdy nespustí cascade pri soft-delete** → strata integrity,
- chýbajúce **ETag / If-Match** → žiadny optimistic-concurrency cez header,
- GraphQL **bez `MaxAllowedComplexity`** → DoS cez nested `first: 10000`,
- FileStorage **bez magic-bytes validácie** ani virus scanningu,
- chýba **`Microsoft.VisualStudio.Threading.Analyzers`** + `<EnforceCodeStyleInBuild>`,
- Dockerfile **beží ako root** (CIS Docker 4.1),
- žiadny **SBOM/Trivy** (EU CRA 2027 risk),
- žiadne **SLO / burn-rate alerty**, default 100% trace sampling,
- per-tenant **log redaction nie je verifikovaná testami** (GDPR risk),
- chýba **DR runbook**, **secrets rotation policy**, **pool sizing** + **statement_timeout**,
- Dragonfly **bez eviction policy** + DataProtection v rovnakom DB (riziko invalid sessions),
- chýbajú **GDPR endpointy** (Art. 15 / 17), **audit log integrity** chain.

**Kde má najväčšie dlhy** (zo základnej analýzy):
- `SharedKernel` je de facto framework — porušuje princíp "shared kernel" a vytvára obrovský coupling,
- nekonzistentná modulová štruktúra (vertical slice vs. clean architecture vs. plochá),
- dva paralelné HTTP štýly (Wolverine HTTP vs. MVC controllery),
- DDD heterogenita (anémický Reviews vs. bohatý ProductCatalog, žiadne `IDomainEvent`/`AggregateRoot`, primitive obsession na ID),
- chýbajúce edge-security (rate limiting nezaregistrované, žiadne security headers, žiadny global body-size limit),
- 8× `Task.Delay` v unit testoch → flaky risk,
- chýbajúci `Contracts/` projekt vyústil v priamu project-reference `ProductCatalog → Reviews`.

---

## 1. Architektúra a moduly

### Štruktúra

- `.slnx` slim solution format, `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true` (`Directory.Build.props:3-7`).
- Centralized Package Management (`Directory.Packages.props:3`).
- 8 modulov (`BackgroundJobs`, `Chatting`, `FileStorage`, `Identity`, `Notifications`, `ProductCatalog`, `Reviews`, `Webhooks`) + `SharedKernel` + `APITemplate.Api` host.

### Modulárna architektúra — najzávažnejšie zistenia

| # | Problém | Lokácia | Závažnosť |
|---|---------|---------|-----------|
| A1 | **`SharedKernel` je framework, nie kernel** — obsahuje `Application/Batch`, `Application/Search`, `Infrastructure/Persistence`, `Infrastructure/Idempotency`, `Infrastructure/OutputCache`, `Contracts/Commands+Events`, `Contracts/Api/ApiControllerBase`. Každá zmena ohrozuje 8 modulov + host. | `src/SharedKernel/Application/*`, `Infrastructure/*`, `Contracts/*` | **High** |
| A2 | **Chýbajúci `Contracts/` projekt** — `slnx` ho deklaruje, ale fyzicky je prázdny. Integration eventy žijú v `SharedKernel/Contracts/Events/` → modul nemôže importovať integration eventy bez referencie celej infra vrstvy. | `src/Contracts/` (prázdny) vs. `implementation_plan.md:194` | **High** |
| A3 | **`ProductCatalog → Reviews` priama project reference** s `global using Reviews.Domain; Reviews.Features;`. Whitelisted v archteste ako "temporary exception". | `src/Modules/ProductCatalog/ProductCatalog.csproj:11`, `GlobalUsings.cs:15-16`, `ModuleBoundaryArchitectureTests.cs:48-58` | **High** |
| A4 | **Nekonzistentná interná štruktúra modulov** — `implementation_plan.md` predpisuje `{Domain, Application, Infrastructure, Api}` per modul, ale realita je plochá zmes (`Domain/`, `Entities/`, `Features/`, `Persistence/`, `Repositories/`, `Services/`, `ValueObjects/`, `Common/`, `Configuration/`, `Configurations/`, `Infrastructure/`, ...). | `src/Modules/ProductCatalog/`, `src/Modules/Reviews/` | **Medium** |
| A5 | **Module registration je tri-handed** — pridanie modulu vyžaduje úpravy v `Program.cs:53-59`, `WolverineModuleDiscovery.cs:19-37`, `HealthCheckModuleRegistry.cs:11`. Porušuje OCP. | viacero | **Medium** |
| A6 | **`MongoException` retry policy v hoste** = leak ProductCatalog detailu do `APITemplate.Api`. | `Program.cs:95` | **Low** |
| A7 | **Žiadne fyzické DB schémy** per modul — všetky moduly do `public` schémy zdieľanej PostgreSQL DB. Logická izolácia len cez DbContext. | `ModuleDbContext.cs` | **Medium** |
| A8 | **`ProductCategoryStats.TotalReviews`** krížom referuje review tabuľku cez stored procedure — krížová DB závislosť maskovaná SQL-om. | `src/Modules/ProductCatalog/Entities/ProductCategoryStats.cs:14` | **Medium** |
| A9 | **GraphQL v ProductCatalog vlastní Reviews mutations** (`ProductReviewMutations.cs`) — presentation-level coupling premietnutý do project reference. | viď A3 | **Medium** |
| A10 | **`InternalsVisibleTo("APITemplate.Tests")`** v každom module + SharedKerneli — testy môžu testovať internals naprieč boundary, podkopáva module isolation. | viacero `.csproj` | **Low** |

### Doplňujúce architektonické zistenia (Structure / Patterns / Data Flow agenty)

| # | Závažnosť | Problém | Lokácia |
|---|-----------|---------|---------|
| A11 | Med | **Poradie module registration vynucované iba komentárom** — žiadna runtime/compile-time ochrana proti zlému poradiu. | `Program.cs:49-51`, `AuthenticationHostingExtensions.cs:17-19` |
| A12 | Med | **Cache invalidation cascades sú staticky príliš široké** — `ForProductDeletion` invaliduje aj Reviews, aj keď nie sú dotknuté. | `CacheInvalidationCascades.cs:4-29` |
| A13 | Med | **Multi-method handlere bez compile-time záruky** správneho stagovania Wolverine pipeline (`LoadAsync` → `HandleAsync`). | `AssignUserRolesCommandHandler.cs:11-66` |
| A14 | Med | **Event handlere objavované cez reflection** — orphaned eventy (bez handlera) sú ticho ignorované, žiadny startup-time check. | `WolverineModuleDiscovery.cs:26-43` |
| A15 | Med | **`KeycloakAdminService.cs` 307 riadkov** — SRP porušenie (user manager + role manager + token provider v jednej triede). | `KeycloakAdminService.cs` |
| A16 | Med | **`ApiServiceCollectionExtensions.cs` 182 riadkov** — mieša validation, caching, Redis, OpenAPI v jednom extension súbore. | `ApiServiceCollectionExtensions.cs:24-109` |
| A17 | Med | **DRY porušenie — tenant-scoped filtering logika opakovaná** v každej module špecifikácii (~10 výskytov). | ProductCatalog/Reviews/Identity Specifications |
| A18 | Low | **`IStoredProcedureExecutor` mieša read + write** — ISP porušenie. | `IStoredProcedureExecutor.cs:8-51` |
| A19 | Low | **`UnitOfWorkForwarder` je čistý forwarding wrapper** bez vlastnej logiky. | `UnitOfWorkForwarder.cs` |
| A20 | Low | **Nekonzistentné interné štruktúry modulov** (ProductCatalog má 17 top-level priečinkov, Chatting iba 1). | `src/Modules/*/` |
| A21 | Low | **`Configuration/` vs `Configurations/` duplicita v ProductCatalog** — kognitívna konfúzia. | `src/Modules/ProductCatalog/` |

### Doplňujúce silné stránky

- **Compiled expression-tree projections** namiesto AutoMappera (`UserMappings.cs:8-24`) — žiadny reflection overhead.
- **Specification pattern konzistentne použitý v 39 špecifikáciách** (Ardalis.Specification) — naprieč ProductCatalog/Reviews/Identity.
- **Korelačné ID preteká celým pipeline** cez Serilog `LogContext` aj response headers (`CorrelationContextMiddleware.cs`).

### Silné stránky architektúry

1. **Wolverine durable outbox/inbox + EF transactions** (`Program.cs:74-87`) — robustný eventual-consistency pattern, pripravený na strangler-fig extrakciu do mikroslužieb.
2. **Per-module DbContext** dediaci `ModuleDbContext` — pekná template-method abstrakcia (audit, tenant filter, soft-delete).
3. **Fluent `ModuleRegistrationBuilder<TContext>`** — eliminuje DI boilerplate naprieč modulmi.
4. **NetArchTest module-boundary tests** (`ModuleBoundaryArchitectureTests.cs`) — automatická enforcement boundary, vrátane explicitnej whitelist.
5. **BFF + Keycloak pattern** s OIDC + cookies + DataProtection na Redis-i.
6. **`ErrorOr` + ProblemDetails RFC 7807** — konzistentný error contract naprieč REST/GraphQL/Wolverine handlers.
7. **Idempotency store** (`Infrastructure/Idempotency/DistributedCacheIdempotencyStore.cs`) — pripravený na exactly-once.
8. **Pokročilý observability stack** (OTel → Alloy → Grafana/Loki/Tempo/Prometheus) v `infrastructure/observability/`.

---

## 2. Kvalita kódu

### Patterns a disciplína

- **CQRS s Wolverine compound handlermi** — `LoadAsync` (preload + validácia, vracia `HandlerContinuation`) + `HandleAsync` (čistá mutácia stavu). Excelentná separácia, vidieť napr. v `Reviews/Features/CreateProductReview/CreateProductReviewCommand.cs:14`, `ProductCatalog/Features/Product/UpdateProducts/UpdateProductsCommand.cs:24`.
- **`ErrorOr<T>`** dôsledne — žiadne business exceptions na control flow (`Rating.Create` vracia `ErrorOr<Rating>`).
- **MEL + source-generated `LoggerMessage`** (76+ partial metód) — alokačne efektívne, structured-logging korektné. Iba 3 súbory používajú surové `_logger.LogX(...)`.
- **Async/await disciplína**: žiadny `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` v `src/`. CT propagovaný všade.
- **Žiadne TODO/FIXME/HACK** komentáre.

### TOP 10 quality issues

| # | Závažnosť | Issue | Lokácia |
|---|-----------|-------|---------|
| Q1 | High | **Anémický `ProductReview`** (`{ get; set; }` všade) vs. bohatý `Product` — nekonzistentný DDD. | `Reviews/Domain/ProductReview.cs:6` |
| Q2 | High | **Dva HTTP štýly**: Reviews = Wolverine HTTP, ProductCatalog = MVC controllery — duplicitné konvencie pre OpenAPI/auth/result-mapping. | `ReviewsHttpEndpoints.cs:16` vs. `ProductsController.CreateProducts.cs:10` |
| Q3 | High | **Žiadne doménové eventy** (`IDomainEvent`/`AggregateRoot`). Cross-module konzistencia rieši synchrónny `bus.InvokeAsync<ValidateProductExistsQuery>` — silná väzba ProductCatalog ↔ Reviews. | `CreateProductReviewCommand.cs:35` |
| Q4 | Med | **Primitive obsession na ID** — všade `Guid` (`UserId`, `ProductId`, `TenantId`); nemožno odlíšiť v signatúre. | `ProductReview.cs:22` |
| Q5 | Med | **`Rating.FromPersistence`/`Price.FromPersistence`** sú `public` a obchádzajú validáciu — mali by byť `internal` + `InternalsVisibleTo` len pre persistence assembly. | `Rating.cs:27`, `UpdateProductsCommand.cs:107` |
| Q6 | Med | **`Product.Name` setter hádže `ArgumentException`** — porušuje "no exceptions for control flow" politiku, zvyšok kódu používa `ErrorOr`. | `Product.cs:12-19` |
| Q7 | Med | **`var` v Identity module** (~15 výskytov) porušuje pravidlo Explicit Typing platné v ostatných moduloch. | `AssignUserRolesCommandHandler.cs:17,37,54` |
| Q8 | Med | **`SoftDeleteProductDataLinks` sa spolieha na implicit persistence-layer chovanie** — len `Remove` z in-memory kolekcie; subtilné a náchylné na regresiu. | `Product.cs:125-129` |
| Q9 | Low | **Duplicitný `DomainErrors` namespace** v SharedKernel a v každom module — nutnosť plne kvalifikovať FQN. | viacero |
| Q10 | Low | **Komentár-as-docs anti-pattern** pre EF Core 10 BindProperty workaround v entite. | `Product.cs:30-36` |

### 5 vzorových best-practices na replikáciu

1. **`Rating` value object** — `readonly record struct`, privátny ctor, `static ErrorOr<Rating> Create`, `FromPersistence` factory pre EF (`Reviews/Domain/Rating.cs:8`). Učebnicový VO.
2. **`UnitOfWork<TContext>`** — execution strategy + savepoint nesting + tracker-state snapshot/restore + per-modul `TDbMarker` (`SharedKernel/Infrastructure/UnitOfWork/UnitOfWork.cs:15`). Production-grade.
3. **`ApiExceptionHandler` + `IHasErrorCode`/`IHasErrorMetadata`** — RFC7807, error-code propagation, client-abort detection (`499`), separátne severity pre 4xx vs 5xx (`ExceptionHandling/ApiExceptionHandler.cs:6`).
4. **Wolverine compound handlery `LoadAsync`/`HandleAsync`** — load-fáza pure-validation, handle-fáza čistá mutácia (`UpdateProductsCommand.cs:24`).
5. **`LoggerMessage` source-generated logging** všade — žiadny string formatting v hot-pathe (`UnitOfWorkLogs.cs`, `IdentityLogs.cs`).

---

## 3. Testovanie a DevEx

### Test stratégia

- **xUnit v3 3.2.2 + Shouldly + Moq + Testcontainers + WebApplicationFactory + NetArchTest + Stryker** — moderný, koherentný stack; žiadny mix Shouldly/FluentAssertions.
- **Single test project** s 3 vrstvami: Unit (144 súborov), Integration (45, opt-in cez `<Compile Include>`), Smoke (sub-set integration). Mutation testing pre SharedKernel.
- Test:source LOC ≈ **0.60** — slušný pomer.
- Solídne fixtures: `IntegrationTestBase<TFactory>`, `CustomWebApplicationFactory`, `IsolatedPostgresDatabase`, `SharedPostgresContainer`, `IntegrationAuthHelper`, `CatalogApiTestHelper`.

### CI/CD

- `.github/workflows/pr-validation.yml` — 2-stage (unit → docker-tests matrix), dorny test-reporter, NuGet cache.
- `.github/workflows/codeql.yml` — C# CodeQL.
- Dependabot prítomný.
- **Chýba**: deploy/release workflow, **CSharpier check v CI** (len pre-commit), **coverage threshold gate** (coverlet collected ale nikdy enforced), **Stryker v CI**, **Qodana scan v CI** (`failThreshold` zakomentované), Windows/macOS test matrix.

### TOP 8 DevEx/testing problémov

| # | Issue | Quick win |
|---|-------|-----------|
| T1 | **8× `Task.Delay` v unit testoch** (BFF Identity), najmä `BffLocalSessionCacheTests.cs:92` čaká 1100 ms | Nahradiť `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) alebo `TaskCompletionSource` synchronizáciou |
| T2 | **Integration testy nie sú pre-built** — `<Compile Remove>` + 40+ explicitných `<Compile Include>` riadkov; krehké pri pridávaní | Buď condition-based glob (`Condition="'$(RunDockerIntegration)' == 'true'"`) alebo separátny `APITemplate.IntegrationTests.csproj` |
| T3 | **Žiadny coverage / mutation gate** v CI | Pridať `coverage-threshold` v `pr-validation.yml` (≥65 %) + nightly Stryker job |
| T4 | **CSharpier nebeží v CI**, len v pre-commit hooku (dá sa obísť) | `dotnet csharpier check .` step do unit jobu |
| T5 | **Žiadne style analyzátory** (StyleCop/Roslynator/Meziantou) | `Roslynator.Analyzers` + `Meziantou.Analyzer` ako `PrivateAssets="all"` |
| T6 | **`RateLimitingTests.cs` zakomentované** od refactoringu | Prepísať alebo zmazať s issue ticketom |
| T7 | **Roztrieštená dokumentácia** — `README.md` 78 KB + 30 súborov v `docs/` + `task.md` + `TODO.md` + `TODO-Architecture.md` + `implementation_plan.md`; žiadny `CONTRIBUTING.md` | Jeden `docs/onboarding.md` s 30-min "first PR" guideom; konsolidovať TODO súbory |
| T8 | **Restore artifakty v repo** (`restore-api.log` 4 MB, `restore-diag.log` 900 KB) | Pridať do `.gitignore` a odstrániť |

### Bonus quick wins

- `RunDockerIntegration` default `false` cez Directory.Build.props (rýchlejšie lokálne `dotnet test`).
- Aspire profile-based docker-compose split (`docker-compose.dev-min.yml` len pg + redis).
- Dependabot grouping pre OpenTelemetry/Wolverine balíčky (menej PR).

---

## 4. Security, performance, observability

### Architektonické základy

- **Dual auth scheme**: JWT Bearer pre M2M + BFF cookie + OIDC pre browser, server-side session storage v Postgres + Redis distributed cache.
- **Authorization**: `PermissionPolicyProvider` + `PermissionAuthorizationHandler` + `[RequirePermission(Permission.X)]` claim-based.
- **Multi-tenancy**: `ITenantProvider` + EF query filters + `TenantId` stĺpec.
- **CSRF**: `CsrfValidationMiddleware` len pre cookie-auth, header `X-CSRF-Token` validovaný per-session.
- **SSRF protection** na outgoing webhookoch (`SsrfProtectedSocketsHttpHandlerFactory`).
- **OpenTelemetry**: tracing + metrics → OTLP → Alloy → Grafana stack. Custom meter pre exception metrics.
- **Wolverine durable inbox/outbox** s Postgres → full transactional outbox.

### TOP 10 rizík

| # | Risk | Impact | Lokácia |
|---|------|--------|---------|
| S1 | **Rate limiting nie je aktivované** — sekcia v `appsettings.json:68-71` existuje, ale `AddRateLimiter`/`UseRateLimiter` nikde v `src/`. DoS / brute-force expozícia | **High** | konfigurácia bez registrácie |
| S2 | **`ExecuteSqlRawAsync` so string-interpoláciou** názvu indexu — SQL injection ak `validIndexes` whitelist nie je vodotesný | **High** | `BackgroundJobs/Services/ReindexService.cs:64-67` |
| S3 | **Žiadne security headers** (HSTS, X-Content-Type-Options, X-Frame-Options, CSP) | **High** | `ApplicationBuilderExtensions.cs:21-67` |
| S4 | **CORS s `AllowCredentials + AllowAnyHeader + AllowAnyMethod`** — origins MUSÍ byť striktný whitelist | Med | `IdentityModule.cs:79-89` |
| S5 | **Žiadny global Kestrel `MaxRequestBodySize`** — `[RequestSizeLimit]` len ad-hoc na `FilesController` (10 MB) a `WebhooksController` (1 MB) | Med | nikde v `src/` |
| S6 | **Dev secrety v repo** (`dev-client-secret`, HMAC key, webhook secret) — riziko copy-paste do prod | Med | `appsettings.Development.json:18,35,40` |
| S7 | **Polly bez circuit breaker / timeout** — len retry. Riziko retry-burst pri downstream zlyhaní | Med | `KeycloakHttpClientBuilderExtensions.cs`, `WebhooksRuntimeBridge.cs:54-75` |
| S8 | **Response compression chýba** — vyšší TTFB, bandwidth pre veľké JSON list odpovede | Med | `ApplicationBuilderExtensions.cs` |
| S9 | **Žiadny OTel log exporter** — Serilog je separátny pipeline; sťažuje koreláciu logov s traces/metrics | Med | `ObservabilityServiceCollectionExtensions.cs` |
| S10 | **`AssumeDefaultVersionWhenUnspecified = true`** môže maskovať klientov bez verzie | Low | `ApplicationCompositionServiceCollectionExtensions.cs:69` |

### TOP 10 zlepšení (s odhadom dopadu)

| # | Improvement | Impact |
|---|-------------|--------|
| 1 | `AddRateLimiter` (FixedWindow/SlidingWindow per IP+user, prísnejšie pre `/auth/*`) | **High** |
| 2 | Security headers middleware (`NetEscapades.AspNetCore.SecurityHeaders`) + `UseHsts()` v non-dev | **High** |
| 3 | Refactor `ReindexService` — hardcoded enum/whitelist + `FormattableString`, žiadna interpolácia | **High** |
| 4 | `AddResponseCompression` (Brotli + Gzip) | Med |
| 5 | Polly `AddCircuitBreaker` + `AddTimeout` k existujúcim retry pipelines | Med |
| 6 | Globálny Kestrel `MaxRequestBodySize` + `FormOptions` limity v `Program.cs` | Med |
| 7 | OTel `WithLogging()` + OTLP log exporter, redukcia dvoch pipelines | Med |
| 8 | Idempotency-Key middleware globálne pre všetky `POST` mutácie cez Wolverine HTTP, nielen ad-hoc atribút | Med |
| 9 | Unify retry policies cez `ResilienceDefaults` na všetky externé HTTP klienty | Low |
| 10 | OpenAPI `OperationId` konvencia + verzionový changelog generator pre lepší klient-codegen | Low |

---

## 5. Architektonické odporúčania na zrýchlenie vývoja a zlepšenie kvality

Skonsolidované odporúčania zoradené podľa **ROI** (impact / effort).

### 🟢 Quick wins (≤ 1 deň, vysoký impact)

1. **Pridať `dotnet csharpier check` + coverage gate ≥65 %** do `pr-validation.yml`. Eliminuje formatting noise a deteguje coverage drop bez ďalších infra zmien.
2. **`AddRateLimiter` + security headers + `UseResponseCompression`** v `ApplicationBuilderExtensions.cs`. Konfigurácia už existuje (`appsettings.json:68-71`), len ju zaregistrovať.
3. **`Roslynator.Analyzers` + `Meziantou.Analyzer`** v `Directory.Packages.props` ako `PrivateAssets="all"`. Bez CI zmien chytí desiatky drobných issues.
4. **Pridať `restore-*.log`, `*.user`, `bin/`, `obj/` do `.gitignore`** a vyčistiť repo (4.9 MB navyše v sledovaných súboroch).
5. **Konsolidovať dokumentáciu**: `CONTRIBUTING.md` + `docs/onboarding.md` (30-min first PR), zjednotiť `TODO.md` + `TODO-Architecture.md` + `task.md` + `implementation_plan.md` do GitHub Issues / Project boardu.
6. **Refactor `ReindexService.cs:64`** na hardcoded `PostgresIndexName` enum — okamžite uzatvorí SQL injection vector.
7. **Nahradiť `Task.Delay` v Identity unit testoch `FakeTimeProvider`-om** — odstráni flaky-risk a skráti lokálny test run o ~1 s na BFF testoch.

### 🟡 Stredne náročné (1–2 týždne, vysoký impact)

8. **Vytvoriť `Contracts/` projekt** podľa `implementation_plan.md` — presunúť `SharedKernel/Contracts/Events`, `Contracts/Commands`, integration DTOs. Tento jeden krok odstráni najtvrdšie napojenie modulov na SharedKernel a umožní strangler-fig extrakciu.
9. **Rozbiť SharedKernel** na 3 projekty:
    - `SharedKernel.Domain` (len value objects, base entity interfaces, `AuditInfo`),
    - `SharedKernel.Application` (CQRS primitives, validation, errors, batch, search),
    - `SharedKernel.Infrastructure` (UnitOfWork, ModuleDbContext, registration, idempotency, redis, output cache).
   Každý modul potom referuje len to, čo skutočne potrebuje. Zníži coupling a zrýchli dirty-build.
10. **Zjednotiť HTTP štýl**: rozhodnúť Wolverine HTTP vs. MVC controllery a zmigrovat ProductCatalog `*Controller.cs` na `[WolverinePost]`/`[WolverineGet]`. Zjednotí OpenAPI tagy, validation pipeline, error mapping a result-shape.
11. **Zaviesť `IModule` interface** s `RegisterServices`/`RegisterEndpoints`/`RegisterHealth`/`RegisterWolverineDiscovery` členmi. Eliminuje 3-handed registráciu a otvorí dvere k auto-discovery cez assembly scan.
12. **Per-module Postgres schémy** (`modelBuilder.HasDefaultSchema("reviews")` v `ModuleDbContext`). Fyzicky vynúti modulové hranice na DB úrovni a uľahčí budúcu DB extrakciu.
13. **Idempotency-Key middleware globálne** pre všetky `POST` mutácie cez Wolverine HTTP — `[Idempotent]` atribút sa stane defaultom.
14. **OTel `WithLogging()` + OTLP log exporter** — zjednotí pipeline logov/traces/metrics, dramaticky zlepší koreláciu pri debug-ovaní cross-module flow.

### 🔴 Veľké iniciatívy (1–3 mesiace, transformačný impact)

15. **Strongly-typed IDs** (`record struct ProductId(Guid Value)`, `UserId`, `TenantId`, ...) — eliminuje primitive obsession (Q4), zachytí celú triedu bugov v compile-time. Prejsť moduly postupne, začať Reviews.
16. **Doménové eventy** (`IDomainEvent` + `AggregateRoot<T>`) namiesto synchrónneho `bus.InvokeAsync<ValidateProductExistsQuery>`. Eventy vyžarovať z aggregátu, dispatchovať cez Wolverine outbox. Začať `ProductSoftDeleted` (už čiastočne existuje) a rozšíriť na `ProductReviewed`, `UserAssignedRole`.
17. **Vyriešiť `ProductCatalog → Reviews` coupling** — presunúť GraphQL Reviews mutations späť do Reviews modulu (s vlastnou GraphQL extension), alebo zaviesť `Contracts.Reviews` s command/DTO kontraktmi. Po vyriešení odstrániť whitelist v `ModuleBoundaryArchitectureTests.cs`.
18. **Bohatý doménový model pre `ProductReview`** — odstrániť anémické `{ get; set; }`, zaviesť factory metódy a invariant enforcement (návod podľa `Product.cs`).
19. **Polly v8 resilience defaults** — jeden `ResilienceDefaults` builder aplikovaný na všetky `IHttpClientFactory` registrácie (Keycloak, webhooky, SMTP wrapped, externé API). Pridať circuit breaker a timeout strategies.
20. **Sub-modul split per modul** (`Reviews.Domain.csproj`, `Reviews.Application.csproj`, `Reviews.Infrastructure.csproj`, `Reviews.Api.csproj`) — fyzicky vynúti acyklickú dependency hranicu predpísanú v `implementation_plan.md`. Jasná predpoveď budúceho mikroslužbového rozdelenia.

---

## 6. Tri varianty implementácie (porovnanie)

Tieto varianty pochádzajú zo separátnej hĺbkovej analýzy (Structure / Patterns&SOLID / Data Flow agenty, viď `ARCHITECTURE_ANALYSIS.md`) a ponúkajú konkrétne implementačné cesty pre architektonické zlepšenie.

### Variant 1 — Targeted Fixes (minimálny, 4–6 h)

**Čo robí**: Rieši iba najkritickejší problém — cross-modul coupling `ProductCatalog → Reviews` — a niekoľko izolovaných quick-winov. Bez väčšieho refaktoru.

**Konkrétne kroky**:
- `SharedKernel.Contracts` → pridať `ICreateProductReviewRequest`, `ProductReviewResponse` DTOs.
- `ProductCatalog.csproj` → odstrániť `<ProjectReference Include="..\Reviews\Reviews.csproj" />`.
- `GlobalUsings.cs:15-16` → odstrániť `using Reviews.Domain`, `using Reviews.Features`.
- `ProductReviewMutations.cs` → prepojiť cez SharedKernel kontrakt (nie priamy Reviews command).
- Zlúčiť `Configuration/` + `Configurations/` v ProductCatalog.

**Predpoklad**: Reviews modul exportuje command cez Wolverine bus alebo SharedKernel kontrakt.

---

### Variant 2 — Structural Hardening ⭐ **odporúčané**

**Čo robí**: Rieši všetky HIGH a MEDIUM problémy systematicky bez zmeny fundamentálnej architektúry. Refaktor udržateľnosti, nie revolúcia.

**Konkrétne kroky**:
- `SharedKernel.Contracts` → zdieľané review DTOs/kontrakty.
- `ProductCatalog.csproj` → odstrániť Reviews reference.
- `SharedKernel/Specifications/TenantScopedSpecification<T>` → nová base class, aktualizovať ~10 špecifikácií.
- `Program.cs` (po `AddModules()`, pred `app.Build()`) → `ValidateModuleRegistrations()` helper, runtime assertion povinných DI registrácií.
- `WolverineModuleDiscovery.cs` → startup validácia: každý `IEvent` typ musí mať aspoň 1 handler (zachytí orphaned eventy).
- `KeycloakAdminService.cs` → rozbiť na `KeycloakUserManager`, `KeycloakRoleManager`, `KeycloakTokenProvider`.
- `ApiServiceCollectionExtensions.cs` → rozbiť na `AddValidationServices`, `AddCachingServices`, `AddOpenApiServices`, `AddRedisServices`.
- `IStoredProcedureExecutor.cs` → rozdeliť na `IStoredProcedureReader` + `IStoredProcedureWriter` (ISP).
- Zjednotenie internej štruktúry modulov podľa kanonického vzoru `{Domain, Application, Infrastructure, Api}`.

**Riešia HIGH+MED issues**: cross-modul coupling (HIGH), fragile registration order (MED), orphaned event handlers (MED), DRY v špecifikáciách (MED), SRP porušenia (MED).

**Predpoklad**: Wolverine umožňuje startup-time handler discovery cez `IWolverineRuntime` (overiť v aktuálnej verzii 5.32).

**Časový odhad**: 2–3 dni.

---

### Variant 3 — Event-Driven Decoupling [BREAKING] (1–2 týždne)

**Čo robí**: Plne event-driven cross-modul komunikácia. Každý cross-modul request ide cez Wolverine bus s explicitne typovanými `*RequestedEvent`/`*CreatedEvent` správami. Cache cascades nahradené domain-event subscription.

**Konkrétne kroky**:
- `SharedKernel.Events` → `ProductReviewRequestedEvent`, `ProductReviewCreatedEvent`.
- `ProductReviewMutations.cs` → posiela `ProductReviewRequestedEvent`, čaká na correlation response.
- Reviews modul → `ProductReviewRequestedEventHandler` → vytvorí review → publishne `ProductReviewCreatedEvent`.
- `CacheInvalidationCascades.cs` → nahradiť statické polia domain-event subscription registráciami (každý event deklaruje, čo invaliduje).
- GraphQL mutations → adaptované pre async response pattern (subscription / polling endpoint).

**Riešia**: kompletná modul independence (foundation pre strangler-fig do mikroslužieb), eliminácia statických cache cascades.

**Riziká**: zásadná zmena API kontraktu pre mutations — synchronný call → asynchrónny event flow. **Major breaking change**.

**Predpoklad**: GraphQL mutations smie vrátiť async response (subscription alebo polling). Wolverine `bus.InvokeAsync<T>` použiteľný pre sync výsledky.

---

### Porovnávacia tabuľka

| Kritérium | V1: Targeted | V2: Structural ⭐ | V3: Event-Driven |
|-----------|-------------|------------------|------------------|
| Komplexita | Nízka | Stredná | Vysoká |
| Úsilie | 4–6 h | 2–3 dni | 1–2 týždne |
| Riziko | Nízke | Nízke | Stredné–Vysoké |
| Udržateľnosť | `+` | `++` | `++` |
| Škálovateľnosť | `0` | `+` | `++` |
| Breaking changes | Žiadne | Minor | Major (mutations API) |
| Rieši HIGH issue | Áno | Áno | Áno |
| Rieši MED issues | Čiastočne | **Všetky** | Všetky + viac |
| Kľúčový kompromis | Iba záplata | Najlepší pomer ROI | Čistá architektúra, ale async mutations sú zlom |

**Odporúčanie**: **Variant 2 (Structural Hardening)** — projekt má zdravú základňu, nepotrebuje revolúciu, potrebuje dôsledné uzavretie otvorených problémov. `TenantScopedSpecification<T>` a Wolverine startup validácia majú vysoký ROI pri minimálnom riziku.

**Poradie implementácie variantu 2**:
1. Odstrániť `ProductCatalog → Reviews` project reference (najprv — odblokuje izoláciu).
2. `TenantScopedSpecification<T>` — vysoký ROI, čistý refaktor bez rizika.
3. Startup validácia registrácií v `Program.cs` — zachytí implicitné závislosti.
4. Rozbiť `KeycloakAdminService` a `ApiServiceCollectionExtensions` — zlepší testovateľnosť.

---

## 7. Plán postupu (odporúčaná postupnosť)

```
Sprint 1 (quick wins, žiadne risk-changes)
├─ #1 CI gates (CSharpier + coverage)
├─ #2 Edge-security (rate limit, headers, compression)
├─ #3 Roslynator + Meziantou analyzéry
├─ #4 .gitignore cleanup
├─ #6 ReindexService SQL fix
└─ #7 FakeTimeProvider v testoch

Sprint 2-3 (foundation pre ďalšiu prácu)
├─ #5 Dokumentačná konsolidácia
├─ #8 Contracts/ projekt
├─ #11 IModule interface
├─ #13 Idempotency middleware
└─ #14 OTel unified pipeline

Sprint 4-6 (architektonické čistenie)
├─ #9 SharedKernel split
├─ #10 HTTP štýl unifikácia
├─ #12 Per-module schémy
├─ #19 Polly resilience defaults
└─ #17 ProductCatalog → Reviews decoupling

Quarter-level iniciatívy (vyžadujú alignment)
├─ #15 Strongly-typed IDs
├─ #16 IDomainEvent + AggregateRoot
├─ #18 ProductReview rich model
└─ #20 Sub-modul split
```

---

## 8. Rozšírená analýza — kódové zlepšenia (nové flagované veci)

Konkrétne kódové problémy s file:line referenciami, ktoré v predchádzajúcich sekciách neboli pokryté.

### EF Core & dáta

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C1 | **Chýba `AsSplitQuery()` / `UseQuerySplittingBehavior`** — `Include`/`ThenInclude` v `ProductByIdWithLinksSpecification.cs`, `ProductsByIdsWithLinksSpecification.cs` generuje kartézsky výbuch | per-spec alebo `DbContextOptionsBuilder.UseNpgsql(o => o.UseQuerySplittingBehavior(SplitQuery))` | Med | S |
| C2 | **Chýba `AddDbContextPool`** — každý request alokuje DbContext nanovo (×6 modulov) | per-modul registration | Med | M |
| C3 | **Žiadne `EnableSensitiveDataLogging` / `ConfigureWarnings`** — Production by mal `ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))` ako N+1 safety-net | `ModuleDbContext.OnConfiguring` | Med | S |
| C4 | **Chýba `TagWith()` na queries** — slow-query traces v PG logoch nemajú origin label | kritické specs | Low | S |
| C5 | **Žiadne EF Core compiled models** — relevantné pre cold-start v 6 DbContextoch | `dotnet ef dbcontext optimize` + `UseModel(...)` | Low | M |
| C6 | **`ModuleDbContext.SetGlobalFilter` cez reflection** (`MakeGenericMethod`+`Invoke`) znemožňuje AOT | `ModuleDbContext.cs:64-69` | Med | M |
| C7 | **ProductCatalog Mongo bez explicitných indexov** — kolekcie pre `ProductData` sú full-scan | `Modules/ProductCatalog/` Mongo persistence | **High** | S |
| C8 | **`OnDelete(DeleteBehavior.SetNull)` na `Product → Category`** nikdy nespustí cascade pri soft-delete (entity nie je `Deleted`, len `IsDeleted=true`) → strata integrity | `ProductConfiguration.cs:31` | **High** | S |

### Wolverine

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C9 | **Žiadne `ScheduleMessage` / `MessageDeduplication` / `StickyHandler`** — ad-hoc delays (TTL pre saga staging, retry s back-offom) by patrili pod Wolverine | rôzne handler call-sites | Med | M |
| C10 | **Chýba Wolverine middleware** (`.Middleware.AddType<...>`) — auditing/tenant resolution sa rieši v každom handleri samostatne (DRY violation) | možný `TenantContextMiddleware` | Med | M |

### HTTP & API contracts

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C11 | **Žiadne ETag / If-Match / If-None-Match** — všetky GET vracajú plné telá, PATCH bez optimistic-concurrency cez header | middleware vytvárajúci ETag z `xmin` (už mapované v `PostgresOptimisticConcurrencyExtensions.cs:17`) | **High** | M |
| C12 | **Žiadne `Sunset` / `Deprecation` headers**, `options.ReportApiVersions = false` | `ApplicationCompositionServiceCollectionExtensions.cs:69` | Low | S |
| C13 | **Žiadne HEAD endpointy** — pre `Files/Download` klient nevie získať veľkosť/hash bez full-body downloadu | FileStorage controller | Med | S |
| C14 | **ProblemDetails bez `instance`** — chýba `context.ProblemDetails.Instance = context.HttpContext.Request.Path` (RFC 9457 §3.1) | `ProblemDetailsErrorTypeConfigureOptions.cs:25-62` | Low | S |

### Validation & GraphQL

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C15 | **DataAnnotations nedokáže vyjadriť cross-field rules** ("Price > 0 ak Status=Active") | 10+ Request DTOs | Med | M |
| C16 | **GraphQL bez `MaxAllowedComplexity`** — len `AddMaxExecutionDepthRule(5)`. DoS cez `first: 10000` na nested fields | `ProductCatalogModule.cs:121` | **High** | S |
| C17 | **Žiadne `UsePersistedOperations`** — production GraphQL klienti by mali persisted query store | `ProductCatalogModule.cs` | Med | M |

### Performance / I/O

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C18 | **`CopyBufferSize = 81920` fixný** — pre 10 MB upload to je 128 syscall round-tripov, lepšie `PipeReader` | `LocalBlobStore.cs:22` | Med | S |
| C19 | **Žiadne magic-bytes / file-signature validácia** — `FileStorageOptions.cs:53` má len allow-list MIME, ktorý posiela klient. Útočník pošle `.exe` s `image/jpeg` | FileStorage upload pipeline | **High** | M |
| C20 | **Žiadny virus scanning** — `LocalBlobStore.WriteStagingAsync` priamo promote bez ClamAV/Defender | možná integrácia `nClam` | **High** | L |

### Notifications & i18n

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C21 | **`FluidEmailTemplateRenderer` resource path bez locale** — `templateName.liquid` jediná verzia, žiadne `welcome.en.liquid` / `welcome.sk.liquid` | `FluidEmailTemplateRenderer.cs:85` | Med | M |
| C22 | **Žiadny bounce-handling pre emaily** — `FailedEmail` zachytáva len SMTP failures, nie hard-bounces (DSN) | nový IMAP listener | Low | L |

### Static analysis & build

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C23 | **Chýba `<EnforceCodeStyleInBuild>true`, `<AnalysisLevel>latest-recommended`, `<AnalysisMode>All`** + `Microsoft.VisualStudio.Threading.Analyzers` | `Directory.Build.props` | **High** | S |
| C24 | **Chýba property-based / snapshot testing** — žiadny FsCheck/Bogus/Verify. `ProblemDetails` shapes a `ErrorOr` mapping by ťažili z snapshot testov | `tests/APITemplate.Tests/` | Med | M |

### Migrations & docs

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| C25 | **Migration files bez konvenčného naming** — mix `S2IdentityQueryPerformanceHardening`, `UseNormalizedStringComplex`, `InitialCasSchema` | per-modul `Migrations/` | Low | S |
| C26 | **Žiadne `HasData()` pre seed-data** — referenčné dáta seed-ované runtime cez `IDatabaseStartupContributor`, nie idempotent voči zmene seed-hodnôt | `DynamicRolesAndPermissions.cs` a iné | Low | M |
| C27 | **Chýba ADR folder** — `docs/` má 25+ MD súborov, ale žiadne `docs/adr/0001-*.md`. Veľké rozhodnutia (Wolverine vs MediatR, Keycloak BFF, Mongo+Postgres polyglot) nezachytené | `docs/adr/` | Med | S |

---

## 9. Rozšírená analýza — operačné a procesné zlepšenia

Konkrétne SRE/DevOps/compliance problémy a riešenia.

### Deployment & Container security

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O1 | **Žiadne K8s manifesty pre API** — len `dragonfly/` v `infrastructure/kubernetes/`. Chýba Deployment, Service, Ingress, HPA, PDB, NetworkPolicy. README sľubuje produkčný template | `infrastructure/kubernetes/` | **High** | M |
| O2 | **Dockerfile beží ako root** — chýba `USER` direktíva (CIS Docker 4.1). .NET base images už majú `app` user (UID 1654) | `src/APITemplate/Api/Dockerfile:17-20` | **High** | S |
| O3 | **`.dockerignore` neúplný** — `COPY ["src/", "./"]` skopíruje `bin/`, `obj/`, `*.log` ak nepokryje | `.dockerignore` | Med | S |
| O4 | **Žiadny SBOM ani vulnerability scanning** v CI — chýba Trivy/Grype, CycloneDX/SPDX. EU CRA 2027 vyžaduje SLSA provenance | `.github/workflows/` | **High** | S |
| O5 | **NuGet `packages.lock.json` chýba** napriek CPM — build na CI a lokálne môžu rozriešiť rôzne tranzitívne verzie | `Directory.Build.props` | Med | S |

### Observability hĺbka

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O6 | **Žiadne SLO / error-budget** — len 4 ploché Prometheus alerty (`5xx > 5%`, `p95 > 750ms`). Hranica padne na malých objemoch už pri 1 chybe | `infrastructure/observability/prometheus/rules/apitemplate-alerts.yml` | **High** | M |
| O7 | **Trace sampler default `AlwaysOn` (100%)** — pri produkčnej záťaži OTLP exportér zaťaží Tempo+sieť | `ObservabilityServiceCollectionExtensions.cs` (chýba `SetSampler(...)`) | Med | S |
| O8 | **Tempo retention 24h** — post-mortem incidentu starého 2 dni nemá traces | `infrastructure/observability/tempo/config.yml:16-18` | Med | S |
| O9 | **Distributed tracing context cez Wolverine bus neoverený** — async message handlery môžu byť „black hole" v trace strome | overiť `.AddSource("Wolverine")` v OTel | Med | S |
| O10 | **Logovanie do file v produkcii** v kontajneri — ephemeral filesystem, dvojitý IO, max 7 MB | `appsettings.Production.json:75-86` (Serilog File sink) | Med | S |
| O11 | **Per-tenant log redaction policy neoverená v testoch** — žiadny snapshot test že email v logoch je `EMAIL_HMAC_xxxx`. GDPR risk | nový test | **High** | M |

### Disaster recovery & secrets

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O12 | **Žiadna DR dokumentácia** — chýba `docs/operations/disaster-recovery.md`, RPO/RTO targets, outbox replay runbook, PITR setup | nový dokument | **High** | M |
| O13 | **Žiadna stratégia secrets rotation** — `KC_CLIENT_SECRET`, `REDACTION_HMAC_KEY`, DataProtection keys (default 90d, bez `ProtectKeysWith*`) | `docker-compose.production.yml:179` + DataProtection setup | **High** | M |

### DB operations

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O14 | **Connection pool sizing nie je explicitný** — default Npgsql pool 100 × N podov → exhaust PG `max_connections` (default 100) | connection string v `docker-compose.production.yml:173` | **High** | M |
| O15 | **Chýba `statement_timeout` / `lock_timeout` na DB úrovni** — runaway query alebo zámok blokujúci pool. Connection string `Options=-c statement_timeout=30000 -c lock_timeout=5000` | connection string | **High** | S |

### Performance & resilience testing

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O16 | **Žiadne load / performance testy** — `tests/load/` neexistuje, žiadny baseline pre alert `p95 > 750ms` | nový k6/NBomber projekt | Med | M |
| O17 | **Žiadny chaos / failure-injection test** — circuit breakers a retries sa nikdy neoverili pod zlými sieťovými podmienkami → falošná istota | `toxiproxy-dotnet` v integration testoch | Med | M |
| O18 | **Žiadne feature flags** — risky deploys = full rollback only. `Microsoft.FeatureManagement` ani `OpenFeature.SDK` v packages | `Directory.Packages.props` | Med | M |

### Multi-env & DX

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O19 | **Chýba `appsettings.Staging.json`** — staging používa Production config (alebo nedefinovane) → late-stage bugy | nový súbor | Med | S |
| O20 | **Žiadny `.devcontainer` ani bootstrap script** — onboarding nového devv = manuálne čítanie 78 KB README | `.devcontainer/devcontainer.json`, `scripts/bootstrap.ps1` | Med | M |
| O21 | **Žiadne PR template, CODEOWNERS, ISSUE_TEMPLATE** — PR review konzistencia padá; vlastníctvo modulov nejasné | `.github/` | Med | S |
| O22 | **Žiadny CHANGELOG ani SemVer release** — `release-please`/`semantic-release`/`Release Drafter` chýba; git tagy chýbajú | nový workflow | Med | S |
| O23 | **Žiadny OpenAPI contract diff / Spectral lint** — breaking changes prejdú nepovšimnuté | `oasdiff/oasdiff-action` + `stoplightio/spectral-action` | Med | S |

### Compliance & GDPR

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O24 | **Žiadna podpora i18n / culture-aware response** — validation atribúty hard-coded EN, žiadny `RequestLocalization` | `services.AddRequestLocalization(["en", "sk", "cs"])` | Low | M |
| O25 | **Tenant onboarding lifecycle nedokumentovaný** — `BootstrapTenant`, `OrphanedProductDataRetentionDays: 7` bez rationale | `docs/tenant-lifecycle.md` + integration test izolácie | **High** | M |
| O26 | **Žiadny GDPR endpoint** — Article 15 (data export) a Article 17 (right-to-be-forgotten) chýbajú | nové endpointy v Identity module | **High** | L |
| O27 | **Audit log integrity nie je chránený** — žiadny hash chain, žiadny WORM storage. SOC2/ISO 27001 audit zlyhá | append-only s `previous_hash` Merkle log alebo S3 Object Lock | **High** | L |

### Cost & operations

| # | Problém | Lokácia | Impact | Effort |
|---|---------|---------|--------|--------|
| O28 | **Dragonfly bez memory eviction policy** — `--maxmemory 512mb` bez `--maxmemory-policy`. Output cache + DataProtection v rovnakom Redis = potential eviction DataProtection keys = invalid sessions | `docker-compose.production.yml:126` + oddeliť DataProtection do `db=1` | **High** | S |

---

## 10. Záver

`API-Template-Monolith` je **nadpriemerne kvalitný .NET 10 modulárny monolit** s vyspelou voľbou stacku a disciplínovaným buildom. Hlavné dlhy nie sú technologické (stack je výborný), ale **architektonické konzistencie**:

- `SharedKernel` zarástlo do frameworku → rozbiť na 3 projekty,
- `Contracts/` projekt je v pláne ale neexistuje → vytvoriť ho,
- modulová štruktúra a HTTP štýl sa líšia medzi modulmi → unifikovať,
- DDD heterogenita (anémia v Reviews, primitive obsession na ID, žiadne doménové eventy) → postupné dorastanie modelu.

**Edge security gaps** (rate limit nezaregistrované, žiadne security headers, žiadny global body limit) sú **kritické a quick-fixovateľné** — odporúčam ich zaradiť do najbližšieho sprintu.

Po vyriešení Sprintov 1–3 bude projekt **pripravený na strangler-fig extrakciu** prvého modulu (Notifications alebo FileStorage) do mikroslužby, ako predpisuje `TODO-Architecture.md`.

---

*Report generovaný 4 paralelnými agentmi. Konkrétne file:line referencie sú overiteľné v repozitári na branchi `feature/critical-tests` (commit `32593ab`).*
