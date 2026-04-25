# Fast Unit Test Audit

This document captures the current state of `tests/APITemplate.Tests` for fast unit-test execution.

## Baseline

- Test project: `tests/APITemplate.Tests/APITemplate.Tests.csproj`
- Runner config: `tests/APITemplate.Tests/xunit.runner.json`
  - `parallelizeTestCollections = true`
  - `maxParallelThreads = 0`
- Unit tests are primarily marked with `[Trait("Category", "Unit")]`.

## Classification

### Fast Unit

Tests that are pure in-memory logic with mocks/fakes only:

- Handler/query tests with `Moq`, `Shouldly`, `ErrorOr`
- Specification/filter/sort tests
- Value object and validator tests
- Middleware tests with `DefaultHttpContext`

These tests should be executable in inner-loop commands.

### Unit.Component (slower unit-level tests)

Tests that stay in `Unit/` but can involve EF InMemory, filesystem temp folders, architecture scans, or connection attempts:

- `tests/APITemplate.Tests/Unit/FileStorage/LocalBlobStoreTests.cs`
- `tests/APITemplate.Tests/Unit/FileStorage/OrphanBlobSweepServiceTests.cs`
- `tests/APITemplate.Tests/Unit/FileStorage/SagaLifecycleIntegrationTests.cs`
- `tests/APITemplate.Tests/Unit/FileStorage/FileUploadSagaTests.cs`
- `tests/APITemplate.Tests/Unit/Identity/PostgresCachedBffSessionStoreTests.cs`
- `tests/APITemplate.Tests/Unit/Identity/PostgresDistributedCacheBffSessionStoreTests.cs`
- `tests/APITemplate.Tests/Unit/Architecture/ModuleBoundaryArchitectureTests.cs`
- `tests/APITemplate.Tests/Unit/Webhooks/SsrfProtectedSocketsHttpHandlerTests.cs`

These tests are parallel-safe but are excluded from the fast-unit target.

## Fast Unit Command

Use this command for quick feedback:

```bash
dotnet test APITemplate.slnx --filter "Category=Unit&Category!=Unit.Component"
```

## Verification Goal

- Fast-unit command should run without Docker/Testcontainers/web host startup.
- `Unit.Component` tests should still run in the full suite and in dedicated component/unit runs.
