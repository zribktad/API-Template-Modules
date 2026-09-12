# Komplexná správa o stave aplikácie, modulárnych Contracts a vyriešených chybách

**Dátum:** 8. september 2026  
**Projekt:** `API-Template-Monolith` (.NET 10 Modular Monolith)  
**Riešenie:** .NET 10, PostgreSQL (EF Core 10), MongoDB, Keycloak, WolverineFx CQRS, HotChocolate GraphQL  

---

## 1. Implementácia vyhradených `Contracts` projektov pre každý komunikujúci modul

Podľa požiadavky bol odstránený antipattern centralizovaného monolitického projektu zmlúv a každý modul komunikujúci s ostatnými modulmi získal vlastný, plne izolovaný `*.Contracts` projekt.

### Vytvorené a nakonfigurované projekty:
1. `src/Contracts/ProductCatalog.Contracts/ProductCatalog.Contracts.csproj`
   - Dotazy a udalosti katalógu produktov: `ValidateProductExistsQuery`, `CleanupOrphanedProductDataCommand`, `ProductsBatchSoftDeletedNotification`.
2. `src/Contracts/Reviews.Contracts/Reviews.Contracts.csproj`
   - Dotazy a DTO recenzií: `GetProductReviewsByProductIdsQuery`, `ProductReviewResponse`.
3. `src/Contracts/Identity.Contracts/Identity.Contracts.csproj`
   - Udalosti a príkazy používateľov a tenantov: `CleanupExpiredBffSessionsCommand`, `CleanupExpiredInvitationsCommand`, `EmailEvents` (`UserRegisteredDomainEvent`, `TenantInvitationCreatedDomainEvent`, `UserRoleChangedDomainEvent`), `SoftDeleteEvents`.
4. `src/Contracts/Notifications.Contracts/Notifications.Contracts.csproj`
   - Príkazy doručovania notifikácií: `RetryFailedEmailsCommand`, `DeadLetterExpiredEmailsCommand`.
5. `src/Contracts/FileStorage.Contracts/FileStorage.Contracts.csproj`
   - Príkaz čistenia neplatných súborov: `SweepOrphanBlobsCommand`.
6. `src/Contracts/BackgroundJobs.Contracts/BackgroundJobs.Contracts.csproj`
   - Zmluvy orchestrátora úloh a dispatchingu.
7. `src/Contracts/Webhooks.Contracts/Webhooks.Contracts.csproj`
   - Príkazy odosielania webhookov: `SendWebhookCallbackCommand`.

### Architektonické zapojenie a pravidlá:
- **Žiadne krížové závislosti medzi implementáciami modulov:** Moduly sa navzájom neodkazujú cez svoje hlavné `.csproj` súbory. Komunikácia prebieha výlučne cez zmluvné typy (`*.Contracts`) a Wolverine `IMessageBus`.
- **Aktualizácia architektúrnych testov (`ModuleBoundaryArchitectureTests.cs`):** Testovanie hraníc modulov bolo upravené tak, aby explicitne povoľovalo závislosti na `*.Contracts` projektoch iných modulov, pričom krížové závislosti na implementačných projektoch sú naďalej striktne zakázané.
- **Overenie kompilácie a testov:**
  - Všetky projekty v `APITemplate.slnx` sa úspešne kompilujú: **`0 Warning(s), 0 Error(s)`**.
  - Všetkých **896 Unit testov** (vrátane architektúrnych testov) úspešne prechádza.

---

## 2. Vyriešené chyby a modernizácie podľa štandardov .NET 10

### 1. Bezpečnosť kontajnera (Dockerfile) – Odstránenie behu pod rootom
- **Stav:** **OPRAVENÉ**
- **Súbor:** `src/APITemplate/Api/Dockerfile`
- **Riešenie:** Do finálneho stage bola doplnená direktíva `USER app`. Kontajner beží pod neprivilegovaným používateľom `app`, čím spĺňa cloud-native security štandardy a bráni container-escape útokom.

### 2. Bezpečnosť GraphQL (DoS a Introspekcia)
- **Stav:** **OPRAVENÉ**
- **Súbory:** `src/APITemplate/Api/Extensions/GraphQLServiceCollectionExtensions.cs`, `Program.cs`
- **Riešenie:** Okrem existujúcej ochrany proti hlbokým a zložitým dopytom (`AddMaxExecutionDepthRule`, `ModifyCostOptions`) bola introspekcia naviazaná na explicitnú konfiguráciu `"GraphQL:EnableIntrospection"`. Tým je schéma chránená v pre-production a staging prostrediach pred únikom informácií.

### 3. Transakčná robustnosť a uvoľňovanie zámkov pri zrušení operácie (CancellationToken)
- **Stav:** **OPRAVENÉ**
- **Súbory:** `src/Modules/Notifications/Domain/FailedEmail.cs`, `src/Modules/Notifications/Services/EmailRetryService.cs`
- **Riešenie:** Do doménovej entity `FailedEmail` bola doplnená metóda `ReleaseClaim()`. V `EmailRetryService` bol blok `catch (OperationCanceledException)` rozšírený o okamžité uvoľnenie zámku (`ReleaseClaim()`) s perzistenciou cez `CancellationToken.None`. Záznamy už neostávajú zablokované celých 15 minút pri bežnom reštarte aplikácie alebo graceful shutedowne.

### 4. Dátová integrita cenových faziet a filtrovania
- **Stav:** **OPRAVENÉ**
- **Súbory:** `ProductCatalog/Features/Product/GetProducts/ProductFilter.cs`, `ProductFilterCriteria.cs`
- **Riešenie:** Do `ProductFilter` bola pridaná podpora pre polootvorené intervaly `PriceLessThanMax`, rešpektujúca presné hranice bucketov `[min, max)` vo fazetách.

### 5. Dátová integrita pri Soft-Delete kategórií
- **Stav:** **OVERENÉ A ZARUČENÉ**
- **Súbor:** `ProductCatalog/Features/Category/DeleteCategories/DeleteCategoriesCommand.cs`
- **Riešenie:** V transakcii mazania kategórií sa striktne volá `productRepository.ClearCategoryAsync(state.CategoryIds, ct)` pred `BulkSoftDeleteByIdsAsync`. Produkty nikdy nezostanú s neplatným odkazom na soft-deletovanú kategóriu.

---

## 3. Rozbor Docker / Rancher named pipe a Testcontainers

- Rancher Desktop / Docker named pipe `\\.\pipe\docker_engine` pod Windows vyžaduje zvýšené práva používateľa.
- 896 unit testov beží úplne nezávisle a prechádza na 100 %. Pre integračné testy stačí spustiť terminál ako Administrátor alebo nastaviť pipe ACL.

---

## 4. Stav riešenia

| Metrika | Výsledok |
|---|---|
| **Kompilácia (dotnet build)** | **0 chýb, 0 varovaní (TreatWarningsAsErrors=true)** |
| **Unit & Architektúrne testy** | **896 / 896 úspešných (100 % pass rate)** |
| **Samostatné Contracts projekty** | **7/7 aktívnych** |
