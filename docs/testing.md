# How to Write Unit and Integration Tests

This guide explains how to add unit tests and integration tests for this project, using the patterns already established in the test suite.

**Test framework:** xUnit
**Assertion library:** Shouldly
**Mocking library:** Moq
**Integration testing:** `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`)
**In-memory database:** `Microsoft.EntityFrameworkCore.InMemory`
**PostgreSQL integration:** Testcontainers.PostgreSql
**Database reset:** Respawn (resets PostgreSQL to a clean state between tests without re-creating the schema)

Run all tests:

```bash
dotnet test
```

---

## Unit Tests

Unit tests live in `tests/APITemplate.Tests/Unit/`. They test a single class in complete isolation — all dependencies are replaced with Moq mocks.

### Service Unit Tests

Services contain business logic. Test them by mocking the repository and `IUnitOfWork`.

**File:** `tests/APITemplate.Tests/Unit/Services/OrderServiceTests.cs`

```csharp
using APITemplate.Application.DTOs;
using APITemplate.Application.Services;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly OrderService _sut;   // sut = system under test

    public OrderServiceTests()
    {
        _repositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sut = new OrderService(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderExists_ReturnsOrderResponse()
    {
        var order = new Order
        {
            Id          = Guid.NewGuid(),
            CustomerId  = Guid.NewGuid(),
            TotalAmount = 99.99m,
            CreatedAt   = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.GetByIdAsync(order.Id);

        result.ShouldNotBeNull();
        result!.TotalAmount.ShouldBe(99.99m);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderDoesNotExist_ReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_PersistsOrderAndReturnsResponse()
    {
        var request = new CreateOrderRequest(Guid.NewGuid(), 49.99m);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order o, CancellationToken _) => o);

        var result = await _sut.CreateAsync(request);

        result.TotalAmount.ShouldBe(49.99m);
        result.Id.ShouldNotBe(Guid.Empty);

        // Verify the repository and UoW were called exactly once
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(
            u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenOrderNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(nameof(Order), Guid.Empty));

        var act = () => _sut.DeleteAsync(Guid.NewGuid());

        await Should.ThrowAsync<NotFoundException>(act);
    }
}
```

> **Pattern:** `_sut` (system under test) is the class being tested. All its dependencies are `Mock<T>` objects created in the constructor.

---

### Validator Unit Tests

Two approaches are used depending on the validation source:

**FluentValidation rules** — call `validator.Validate()` directly:

```csharp
// tests/APITemplate.Tests/Unit/Validators/CreateOrderRequestValidatorTests.cs
using APITemplate.Application.DTOs;
using APITemplate.Application.Validators;
using Shouldly;
using Xunit;

public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _sut.Validate(new CreateOrderRequest(Guid.NewGuid(), 49.99m));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NegativeOrZeroAmount_FailsValidation(decimal amount)
    {
        var result = _sut.Validate(new CreateOrderRequest(Guid.NewGuid(), amount));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "TotalAmount");
    }

    [Fact]
    public void EmptyCustomerId_FailsValidation()
    {
        var result = _sut.Validate(new CreateOrderRequest(Guid.Empty, 10m));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CustomerId");
    }
}
```

**Data annotation rules** (`[Required]`, `[MaxLength]`, `[Range]`) — use `System.ComponentModel.DataAnnotations.Validator`:

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
public void Annotation_InvalidName_IsInvalid(string? name)
{
    var request = new CreateProductRequest(name!, null, 9.99m);
    var results = new List<ValidationResult>();

    var isValid = Validator.TryValidateObject(
        request, new ValidationContext(request), results, validateAllProperties: true);

    isValid.ShouldBeFalse();
    results.ShouldContain(r => r.MemberNames.Contains("Name"));
}
```

---

### Repository Unit Tests

Repository tests use `EF Core InMemory` — no Moq needed here because EF itself is very lightweight. Be aware that the InMemory provider does *not* behave like a relational database (e.g., it does not enforce all constraints, handles transactions differently, and some queries may translate differently), so prefer a relational provider such as SQLite in-memory or Testcontainers-based database tests when you need higher-fidelity integration or query/constraint behavior.

```csharp
// tests/APITemplate.Tests/Unit/Repositories/OrderRepositoryTests.cs
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

public class OrderRepositoryTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly OrderRepository _sut;

    public OrderRepositoryTests()
    {
        // Each test gets its own isolated InMemory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _sut = new OrderRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task AddAsync_PersistsOrder()
    {
        var order = NewOrder(99.99m);

        await _sut.AddAsync(order);
        await _dbContext.SaveChangesAsync();   // simulate UnitOfWork.CommitAsync()

        var persisted = await _dbContext.Orders.FindAsync(order.Id);
        persisted.ShouldNotBeNull();
        persisted!.TotalAmount.ShouldBe(99.99m);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ThrowsNotFoundException()
    {
        var act = () => _sut.DeleteAsync(Guid.NewGuid());

        await Should.ThrowAsync<NotFoundException>(act);
    }

    private static Order NewOrder(decimal amount) => new()
    {
        Id          = Guid.NewGuid(),
        CustomerId  = Guid.NewGuid(),
        TotalAmount = amount,
        CreatedAt   = DateTime.UtcNow
    };
}
```

> **Isolation:** Pass `Guid.NewGuid().ToString()` as the database name so every test class runs against a fresh, empty in-memory store.

---

### Middleware Unit Tests

Middleware tests build a `DefaultHttpContext` manually — no HTTP server needed. The project uses this pattern in `RequestContextMiddlewareTests`:

```csharp
// tests/APITemplate.Tests/Unit/Middleware/RequestContextMiddlewareTests.cs
[Fact]
public async Task InvokeAsync_WhenHeaderProvided_EchoesCorrelationIdToResponse()
{
    var middleware = new RequestContextMiddleware(async ctx => await ctx.Response.WriteAsync("ok"));
    var context = new DefaultHttpContext();
    context.Response.Body = new MemoryStream();
    context.Request.Headers[RequestContextMiddleware.CorrelationIdHeader] = "corr-123";

    await middleware.InvokeAsync(context);

    context.Response.Headers[RequestContextMiddleware.CorrelationIdHeader].ToString().ShouldBe("corr-123");
    context.Response.Headers["X-Trace-Id"].ToString().ShouldNotBeNullOrWhiteSpace();
    context.Response.Headers["X-Elapsed-Ms"].ToString().ShouldNotBeNullOrWhiteSpace();
}
```

Exception translation behavior is covered separately in `tests/APITemplate.Tests/Unit/ExceptionHandling/ApiExceptionHandlerTests.cs`.

---

## Integration Tests

Integration tests live in `tests/APITemplate.Tests/Integration/`. They start the full ASP.NET Core pipeline in memory using `WebApplicationFactory`, with the real DI container and middleware — only the databases are swapped out.

### The `CustomWebApplicationFactory`

All integration test classes share `CustomWebApplicationFactory`, which delegates to helpers in `TestServiceHelper`:

| Helper | What it replaces |
|--------|-----------------|
| `MockMongoServices` | Removes `MongoDbContext`; mocks `IProductDataRepository` |
| `RemoveExternalHealthChecks` | Removes `postgresql`, `mongodb`, `keycloak`, `dragonfly` health registrations |
| `ReplaceOutputCacheWithInMemory` | Swaps DragonFly-backed output cache for in-memory |
| `ReplaceDataProtectionWithInMemory` | Replaces DragonFly-backed DataProtection with `EphemeralDataProtectionProvider` (no key persistence) |
| `ReplaceTicketStoreWithInMemory` | Replaces Redis-backed `IDistributedCache` with in-memory; re-registers `DragonflyTicketStore` against it |
| `ConfigureTestAuthentication` | Overrides JWT validation to use a local RSA test key; stubs the OIDC metadata |

```csharp
// tests/APITemplate.Tests/Integration/CustomWebApplicationFactory.cs
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(TestConfigurationHelper.GetBaseConfiguration()));

        builder.ConfigureTestServices(services =>
        {
            // Swap PostgreSQL for InMemory
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase(_dbName));

            TestServiceHelper.MockMongoServices(services);
            TestServiceHelper.RemoveExternalHealthChecks(services);
            TestServiceHelper.ReplaceOutputCacheWithInMemory(services);
            TestServiceHelper.ReplaceDataProtectionWithInMemory(services);
            TestServiceHelper.ReplaceTicketStoreWithInMemory(services);
            TestServiceHelper.ConfigureTestAuthentication(services);
        });

        builder.UseEnvironment("Development");
    }
}
```

### BFF / CSRF Integration Tests

Tests that need to simulate a **cookie-authenticated** session extend `CustomWebApplicationFactory` with `BffSecurityWebApplicationFactory`. It registers a `FakeCookieAuthStartupFilter` that, when the request includes `X-Test-Cookie-Auth: 1`, pre-populates `HttpContext.User` with an identity whose `AuthenticationType` equals `BffAuthenticationSchemes.Cookie`. Because `UseAuthentication` does not overwrite a pre-set user when the default JWT Bearer scheme finds no token, the CSRF middleware sees the correct cookie identity.

```csharp
// tests/APITemplate.Tests/Integration/BffSecurityTests.cs
public sealed class BffSecurityTests : IClassFixture<BffSecurityWebApplicationFactory>
{
    [Fact]
    public async Task PostWithCookieAuth_WithoutCsrfHeader_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");

        var response = await client.PostAsync("/api/v1/products",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

> **CSRF contract:** SPA clients must call `GET /api/v1/bff/csrf` on startup to retrieve the required header (`X-CSRF: 1`) and include it on every non-safe mutation request authenticated with the session cookie.

---

### REST Integration Test Pattern

Use `IClassFixture<CustomWebApplicationFactory>` to share one factory instance across all tests in a class:

```csharp
// tests/APITemplate.Tests/Integration/OrdersControllerTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

public class OrdersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public OrdersControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullCrudFlow_CreateAndDelete()
    {
        await AuthenticateAsync();

        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/v1/orders",
            new { customerId = Guid.NewGuid(), totalAmount = 79.99 });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("id").GetString();
        orderId.ShouldNotBeNullOrWhiteSpace();

        // Read
        var getResponse = await _client.GetAsync($"/api/v1/orders/{orderId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        fetched.GetProperty("totalAmount").GetDecimal().ShouldBe(79.99m);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/v1/orders/{orderId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify gone
        var gone = await _client.GetAsync($"/api/v1/orders/{orderId}");
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task AuthenticateAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = "default\\admin", Password = "admin" });

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("accessToken").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
```

> **Credentials:** The demo credentials are `default\admin` / `admin` (set in `appsettings.Development.json` or injected by the test environment).

---

### GraphQL Integration Test Pattern

GraphQL is tested by POSTing JSON to `/graphql`. A shared helper method `PostGraphQLAsync` handles serialisation:

```csharp
// tests/APITemplate.Tests/Integration/OrderGraphQLTests.cs
public class OrderGraphQLTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrderGraphQLTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_GetOrders_ReturnsEmptyList()
    {
        var query = new { query = "{ orders { nodes { id totalAmount } } }" };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content
            .ReadFromJsonAsync<GraphQLResponse<OrdersData>>(GraphQLJsonOptions.Default);

        result!.Data.Orders.Nodes.Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GraphQL_CreateOrder_RequiresAuthentication()
    {
        var mutation = new
        {
            query = @"
                mutation($input: CreateOrderRequestInput!) {
                    createOrder(input: $input) { id totalAmount }
                }",
            variables = new { input = new { customerId = Guid.NewGuid(), totalAmount = 9.99 } }
        };

        var response = await PostGraphQLAsync(mutation);

        // HotChocolate returns 200 with an error object (not 401) for unauthenticated mutations
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("errors");
    }

    private async Task AuthenticateAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = "default\\admin", Password = "admin" });

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("accessToken").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }
}

// Response model helpers (add to a shared file or inline):
public sealed record OrderItem(Guid Id, decimal TotalAmount);
public sealed record OrderConnection(List<OrderItem> Nodes);
public sealed record OrdersData(OrderConnection Orders);
```

---

### What to Test at Each Layer

| Layer | Test type | Key assertions |
|-------|-----------|----------------|
| Service | Unit | Business logic paths, `NotFoundException`, `CommitAsync` called |
| Validator | Unit | Valid/invalid inputs, cross-field rules, error property names |
| Repository | Unit (InMemory) | CRUD operations, `NotFoundException` on missing ID |
| Middleware | Unit | Correct HTTP status codes, JSON error body |
| Controller (REST) | Integration | HTTP status codes, response shape, auth enforcement |
| GraphQL resolvers | Integration | `data` field shape, `errors` field on auth failure |

---

## Running Tests

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "FullyQualifiedName~Unit"

# Run only integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Run a single test class
dotnet test --filter "FullyQualifiedName~ProductServiceTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

---

## Hybrid Integration Strategy (InMemory + PostgreSQL)

The suite uses a hybrid approach:

- **InMemory integration tests** for fast feedback on HTTP pipeline, auth, middleware, and endpoint contracts.
- **PostgreSQL Testcontainers integration tests** for real migration/function behavior
  (for example row version updates and tenant-scoped SQL function behavior).

### Requirements for PostgreSQL integration tests

1. Docker daemon must be running (Testcontainers starts/stops PostgreSQL automatically).
2. Do not run app under debugger while running tests, otherwise `APITemplate.dll` may be file-locked.
3. **Respawn** resets all table data between individual test runs (via `Respawner.ResetAsync()`) without dropping or re-creating the schema, so migrations run only once per test session.

### Targeted execution commands

```bash
# Fast integration suite (excludes PostgreSQL-tagged tests)
dotnet test --filter "FullyQualifiedName~Integration&Category!=Integration.Postgres"

# PostgreSQL-only integration suite
dotnet test --filter "Category=Integration.Postgres"

# Full suite
dotnet test
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `tests/APITemplate.Tests/Integration/CustomWebApplicationFactory.cs` | Replaces databases for integration tests |
| `tests/APITemplate.Tests/Integration/GraphQLResponse.cs` | Generic GraphQL response wrapper |
| `tests/APITemplate.Tests/Integration/GraphQLJsonOptions.cs` | Shared `JsonSerializerOptions` for GraphQL responses |
| `tests/APITemplate.Tests/Integration/IntegrationAuthHelper.cs` | Shared tenant-aware login/seeding helper for integration tests |
| `tests/APITemplate.Tests/Integration/Postgres/PostgresWebApplicationFactory.cs` | PostgreSQL Testcontainers host factory |
| `tests/APITemplate.Tests/Integration/Postgres/PostgresDataIntegrityTests.cs` | High-fidelity PostgreSQL integration tests |
| `tests/APITemplate.Tests/Unit/Services/ProductServiceTests.cs` | Service unit test example |
| `tests/APITemplate.Tests/Unit/Repositories/ProductRepositoryTests.cs` | Repository unit test example |
| `tests/APITemplate.Tests/Unit/Validators/CreateProductRequestValidatorTests.cs` | Validator test example |
| `tests/APITemplate.Tests/Integration/AuthenticatedCrudTests.cs` | Full REST CRUD integration test example |
| `tests/APITemplate.Tests/Integration/GraphQLTests.cs` | GraphQL integration test example |

