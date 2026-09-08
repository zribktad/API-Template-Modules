# Komplexná správa o stave aplikácie, modulárnych Contracts a identifikovaných chybách

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

## 2. Rozbor Docker / Rancher named pipe a Integračných testov

Pôvodných 167 zlyhaní pri spustení `dotnet test` bolo spôsobených zlyhaním Testcontainers:

### Príčina:
Rancher Desktop / Docker démon beží na hostiteľskom systéme Windows, no named pipe `\\.\pipe\docker_engine` má nastavené ACL prístupové práva vyžadujúce špecifické administrátorské oprávnenia. Proces bežiaci v neadministrátorskom kontexte dostáva `Access is denied` / `DockerUnavailableException: Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'`.

### Riešenie:
- Ak sa testy spúšťajú v bežnom vývojovom procese bez administrátorských práv na Docker pipe:
  ```powershell
  dotnet test tests/APITemplate.Tests/APITemplate.Tests.csproj --no-build --filter "Category=Unit"
  ```
  *(Výsledok: 896 Passed, 0 Failed).*
- Pre spustenie Testcontainers integračných testov je potrebné spustiť terminál ako Administrátor alebo nastaviť práva pre named pipe cez `icacls \\.\pipe\docker_engine /grant "Users:F"`.

---

## 3. Katalóg identifikovaných chýb a technických zraniteľností v aplikácii

Hĺbkovou analýzou zdrojových kódov a biznis logiky bolo identifikovaných viacero závažných implementačných chýb:

### 1. Dátová integrita: Soft-Delete Cascade vs. Relačný `DeleteBehavior.SetNull`
- **Kde:** `ProductCatalog/Configurations/ProductConfiguration.cs` a `Entities/Category.cs`
- **Chyba:** Vzťah `Product -> Category` je v EF Core nakonfigurovaný s `OnDelete(DeleteBehavior.SetNull)`. Táto kaskáda na úrovni PostgreSQL funguje výhradne pri fyzickom `DELETE`. V aplikácii sa však kategórie mažu logicky (soft-delete, `IsDeleted = true`).
- **Následok:** Po soft-delete kategórie zostávajú produkty s neplatným `CategoryId` ukazujúcim na zmazanú kategóriu. V dotazoch, ktoré aplikujú globálny query filter na `Category`, vznikajú tiché anomálie (napr. zlyhania INNER JOINov alebo prázdne kategórie pri produktoch).
- **Oprava:** Pri soft-delete kategórie v `CategoryRepository` alebo cez udalosť `CategorySoftDeletedDomainEvent` je nutné explicitne spustiť `ClearCategoryAsync(categoryIds)` na produktoch.

### 2. Dátová anomália: Nekonzistentné hranice v cenových fazetách (Bucket Edge Cases)
- **Kde:** `ProductCatalog/Repositories/ProductRepository.cs` (`GetPriceFacetsAsync`)
- **Chyba:** Rozsahy v lambda výrazoch sú definované ako:
  `product.Price >= 0m && product.Price < 50m`, `product.Price >= 50m && product.Price < 100m`, atď.
  Kým popisky a intervaly používajú polootvorené intervaly `[min, max)`, v textovom vyhľadávaní (`ProductFilterCriteria.cs`) sa pre filtrovanie používa `p.Price <= filter.MaxPrice.Value` (uzavretý interval).
- **Následok:** Produkt s cenou presne `50.00` spadne do fazety `50 to <100`. Ak však používateľ klikne na filter `MaxPrice = 50`, SQL dotaz vráti produkt, ale fazeta mu priradí iný bucket.

### 3. Bezpečnosť: Zraniteľnosť GraphQL voči Denial-of-Service (DoS)
- **Kde:** HotChocolate konfigurácia v `GraphQLServiceCollectionExtensions.cs`
- **Chyba:** V starších verziách chýbali limity; bolo overené, že boli doplnené `AddMaxExecutionDepthRule` a `ModifyCostOptions`, avšak introspekcia je podmienená iba prostredím `!environment.IsDevelopment()`. V staging/pre-production prostrediach môže dôjsť k úniku celej schémy a typov.
- **Oprava:** Konfiguráciu introspekcie a povolených operácií riadiť cez explicitné nastavenie v `appsettings.json` namiesto výhradného spoliehania sa na názov prostredia.

### 4. Bezpečnosť kontajnera: Dockerfile bežiaci pod `root` účtom
- **Kde:** `src/APITemplate/Api/Dockerfile`
- **Chyba:** Záverečný stage `final` nemá direktívu `USER app`.
- **Riziko:** Ak by došlo k zraniteľnosti typu Remote Code Execution (RCE) v aplikácii alebo niektorej knižnici, útočník získa plný root prístup v rámci kontajnera, čo výrazne uľahčuje únik z kontajnera (container breakout).
- **Oprava:** Pridať `USER app` pred `ENTRYPOINT`.

### 5. Architektonická čistota: Zdieľaná predvolená schéma `public`
- **Kde:** Všetky moduly (`IdentityDbContext`, `ProductCatalogDbContext`, `NotificationsDbContext`, `ReviewsDbContext`)
- **Chyba:** Všetky `DbContext` inštancie generujú tabuľky do predvolenej schémy `public`.
- **Riziko:** Možnosť kolízie názvov tabuliek, absencia databázovej izolácie medzi modulmi a komplikovanejšia správa oprávnení v PostgreSQL.
- **Oprava:** V každom module v `OnModelCreating` nastaviť vyhradenú schému cez `builder.HasDefaultSchema("catalog")`, `builder.HasDefaultSchema("identity")`, atď.

### 6. Transakčná robustnosť: Uvoľňovanie zámkov pri zlyhaní SMTP
- **Kde:** `Notifications/Services/EmailRetryService.cs`
- **Chyba:** `FailedEmail` záznamy sú zamykané cez `ClaimedUntilUtc`. Ak počas odosielania dôjde k pádu procesu alebo nekontrolovanému ukončeniu vlákna, záznam zostáva zamknutý až do vypršania lease času (napr. 15 minút), aj keď proces už nebeží.
- **Oprava:** Zaviesť heartbeat alebo explicitné uvoľnenie zámku v `finally` bloku pri zachytení nezotaviteľnej výnimky.

---

## 4. Stav repozitára a zhrnutie

| Oblasť | Stav pred zmenou | Aktuálny stav |
|---|---|---|
| **Contracts architektúra** | Centralizované v `SharedKernel` | **7 samostatných projektov `src/Contracts/*.Contracts`** |
| **Kompilácia solution** | Zlyhávala na multi-process MSBuild a NuGet audite | **Úspešná (0 chýb, 0 varovaní)** |
| **Unit testy** | 896 prechádzalo | **896 prechádza (100 % úspešnosť)** |
| **Architektúrne testy** | Striktné obmedzenie | **Aktualizované pre podporu `*.Contracts`** |
