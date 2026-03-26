# How-To Guides

Step-by-step workflow guides for this API template. Each guide covers a complete end-to-end example you can follow to extend the project.

| Guide | What it covers |
|-------|----------------|
| [GraphQL Endpoint](graphql-endpoint.md) | Create a type, query, mutation, and DataLoader with HotChocolate |
| [REST Endpoint](rest-endpoint.md) | Full workflow: entity → DTO → validator → Wolverine handler → controller |
| [Endpoint Showcases](endpoint-showcase.md) | SSE streaming, file upload/download, async jobs, batch, idempotency, JSON Patch, webhooks |
| [EF Core Migration](ef-migration.md) | Add and apply PostgreSQL schema migrations with EF Core |
| [MongoDB Migration](mongodb-migration.md) | Create index and data migrations with Kot.MongoDB.Migrations |
| [Transactions](transactions.md) | Wrap multiple operations in an atomic Unit of Work transaction |
| [Authentication](AUTHENTICATION.md) | JWT login flow, protecting endpoints, and production guidance |
| [Stored Procedures](stored-procedures.md) | Add a PostgreSQL function and call it safely from C# |
| [MongoDB Polymorphism](mongodb-polymorphism.md) | Store multiple document subtypes in a single MongoDB collection |
| [Validation](validation.md) | Add FluentValidation rules, cross-field rules, and shared validators |
| [Specifications](specifications.md) | Write reusable EF Core query specifications with Ardalis.Specification |
| [Scalar & GraphQL UI](scalar-and-graphql-ui.md) | Use the Scalar REST explorer and Nitro GraphQL playground |
| [Testing](testing.md) | Write unit tests (services, validators, repositories) and integration tests |
| [Observability](observability.md) | Run OpenTelemetry locally with Aspire Dashboard or Grafana LGTM |
| [Caching](CACHING.md) | Configure output caching, rate limiting, and DragonFly backing store |
| [Result Pattern](result-pattern.md) | Guidelines for introducing selective `Result<T>` flow in phase 2 |
| [Git Hooks](GIT_HOOKS.md) | Auto-install Husky.Net hooks and format staged C# files with CSharpier |
