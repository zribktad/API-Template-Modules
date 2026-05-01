# APITemplate.Tests — AGENTS.md

## OVERVIEW
xUnit v3 test suite with Unit tests (Moq), Integration tests (WebApplicationFactory + in-memory DB), and Testcontainers PostgreSQL tests. Selective Compile Include pattern gates integration test compilation.

## STRUCTURE
```
APITemplate.Tests/
├── Unit/
│   ├── Services/           # Service business logic (Moq)
│   ├── Repositories/       # Repository filtering/query logic (Moq)
│   ├── Validators/         # FluentValidation.TestHelper
│   ├── Middleware/         # Middleware behavior tests
│   ├── ExceptionHandling/  # ApiExceptionHandler errorCode mapping
│   ├── Identity/           # Identity module unit tests (39 files)
│   ├── ProductCatalog/     # ProductCatalog unit tests (14 files)
│   ├── Notifications/      # Notifications unit tests (8 files)
│   └── FileStorage/        # FileStorage unit tests (9 files)
├── Integration/            # WebApplicationFactory + in-memory DB
│   ├── Postgres/           # Testcontainers PostgreSQL (real DB fidelity)
│   └── Features/           # Feature-level integration tests (9 files)
├── APITemplate.Tests.csproj
└── (test utilities, fixtures)
```

## WHERE TO LOOK
| Need | Location |
|------|----------|
| Unit test a service | `Unit/Services/` |
| Unit test a validator | `Unit/Validators/` |
| Unit test repository | `Unit/Repositories/` |
| HTTP round-trip test | `Integration/` |
| Real PostgreSQL test | `Integration/Postgres/` |
| Test identity logic | `Unit/Identity/` |
| Testcontainers setup | `Integration/Postgres/` fixtures |

## CONVENTIONS
- **xUnit v3** — not v2. Use `[Fact]`, `[Theory]`, `IAsyncLifetime` for setup/teardown.
- **Moq** for mocking — `Mock<T>`, `Setup()`, `Verify()`.
- **Shouldly** for assertions — `result.ShouldBe(expected)`, not `Assert.Equal`.
- **FluentValidation.TestHelper** — `validator.ShouldHaveValidationErrorFor(x => x.Property, value)`.
- **CustomWebApplicationFactory** replaces Npgsql with `UseInMemoryDatabase`, removes `MongoDbContext`, mocks `IProductDataRepository`.
- Each factory instance gets its own isolated in-memory DB (GUID-based name).

## SELECTIVE COMPILE INCLUDE
The `.csproj` excludes all `Integration/**/*.cs` by default, then explicitly re-includes specific files:
```xml
<Compile Remove="Integration\**\*.cs" />
<!-- Then specific files re-included one-by-one -->
<Compile Include="Integration\Postgres\SpecificTest.cs" />
```
This gates which integration tests compile — likely to manage Docker/Testcontainers dependencies.

## TEST CATEGORIES
| Category | Filter | Use Case |
|----------|--------|----------|
| `Unit` | `Category=Unit&Category!=Unit.Component` | Fast inner-loop tests |
| `Unit.Component` | `Category=Unit.Component` | Slower component-style tests |
| `Integration` | `Category=Integration` | In-memory DB, no external deps |
| `Integration.Postgres` | `Category=Integration.Postgres` | Requires Docker (Testcontainers) |
| `Smoke` | `Category=Smoke` | Quick sanity checks |

## ANTI-PATTERNS
- NEVER adapt production code to satisfy tests — tests verify intended behavior.
- NEVER share state between tests — each test gets its own in-memory DB instance.
- NEVER skip Testcontainers tests without Docker — they require a running Docker daemon.
- NEVER add integration test files without updating `.csproj` selective include.

## NOTES
- `InternalsVisibleTo("APITemplate.Tests")` on every project — white-box testing across entire solution.
- Integration tests use `WebApplicationFactory<Program>` — `Program` is `public partial class` for this reason.
- Testcontainers PostgreSQL tests verify tenant isolation and transaction behavior against real PG.
