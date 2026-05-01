# Caching

## Overview

The application uses **ASP.NET Core Output Cache** with an optional **DragonFly** (Redis-compatible) backing store to cache HTTP GET responses. DragonFly supports a master/replica topology with HAProxy for high availability in Docker Compose and a Kubernetes operator for automated failover.

- **With DragonFly configured** — all application instances share the same cache, ensuring consistency behind a load balancer.
- **Without DragonFly** — falls back to in-memory cache with an informational log. Suitable for local development and single-instance deployments.

> **BFF session caching is a separate concern.** Authenticated BFF sessions are cached in a two-tier layout (per-instance L1 `IMemoryCache` + shared L2 Redis) with Redis pub/sub invalidation for cross-instance coherence. That layer is documented in [AUTHENTICATION.md § 3e](AUTHENTICATION.md#3e-storage-architecture-and-redis-cache-keys); this document covers **output caching** for HTTP GET responses only.

## Architecture

### Single Instance (Development)

```
Client Request
       │
       ▼
  Authentication
       │
       ▼
  Authorization
       │
       ▼
  Rate Limiting ─────────────── See [RATE_LIMITING.md](RATE_LIMITING.md)
       │
       ▼
  Output Cache Middleware ──── Redis/DragonFly (shared store)
       │                         ▲
       ▼                         │
  Controller ────────────────────┘
  (EvictByTagAsync on mutations)
```

### Multi-Instance (Docker Compose HA)

```
Client Request
       │
       ▼
  ASP.NET Core API
       │
       ▼
  HAProxy (dragonfly-proxy:6379)
       │
       ├── dragonfly-master:6379  (read/write)
       │
       └── dragonfly-replica:6379 (backup, read-only)
```

HAProxy routes all traffic to the master. If the master fails, HAProxy falls back to the replica (read-only — reads work, writes fail). For automatic master promotion, use the Kubernetes operator.

### Kubernetes (Operator-managed HA)

```
Client Request
       │
       ▼
  ASP.NET Core API
       │
       ▼
  dragonfly.apitemplate.svc.cluster.local:6379
       │
       ▼
  DragonFly Operator
       ├── master pod  (auto-promoted on failure)
       └── replica pod
```

The Output Cache middleware runs **after** authentication, authorization, and [rate limiting](RATE_LIMITING.md), so rejected requests are handled before reaching the cache layer.

---

## Configuration

### Redis / DragonFly Connection

Distributed caching is configured via the `Redis` section in `appsettings.json`. The same connection is shared by Output Cache, Distributed Cache, and Data Protection key storage.

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "ConnectTimeoutMs": 5000,
    "SyncTimeoutMs": 3000
  }
}
```

| Setting            | Default | Description                                                                 |
| ------------------ | ------- | --------------------------------------------------------------------------- |
| `ConnectionString` | `""`    | Redis connection string (StackExchange.Redis format). Leave empty for memory fallback. |
| `ConnectTimeoutMs` | `5000`  | Connection timeout in milliseconds.                                         |
| `SyncTimeoutMs`    | `3000`  | Synchronous operation timeout in milliseconds.                              |

When the `Redis:ConnectionString` setting is **missing or empty**, the application logs an informational message and uses the built-in in-memory distributed cache. No DragonFly instance is required for local development.

### Infrastructure Registration

The `AddRedisInfrastructure(configuration)` extension method (in `RedisServiceCollectionExtensions.cs`) centralizes Redis setup:
1. **Validates options** at startup.
2. **Registers `IConnectionMultiplexer`** as a singleton with lazy connection (doesn't block startup).
3. **Registers Distributed Cache** (`AddStackExchangeRedisCache`).
4. **Registers Data Protection** to persist XML keys in Redis (key: `DataProtection-Keys`).

### Cache Policies

Defined in `ApiServiceCollectionExtensions.cs`:

| Policy       | Expiration | Tag          | Used By                                  |
| ------------ | ---------- | ------------ | ---------------------------------------- |
| *(base)*     | No cache   | —            | All endpoints by default                 |
| `Products`   | 30 seconds | `Products`   | `ProductsController` GET endpoints       |
| `Categories` | 60 seconds | `Categories` | `CategoriesController` GET endpoints     |
| `Reviews`    | 30 seconds | `Reviews`    | `ProductReviewsController` GET endpoints |

The base policy disables caching for all endpoints. Only endpoints explicitly decorated with `[OutputCache(PolicyName = "...")]` are cached.

> **Important:** By default ASP.NET Core Output Cache does not cache responses that carry an `Authorization` header. All named policies include `TenantAwareOutputCachePolicy` (see [Tenant Isolation](#tenant-isolation)) which overrides this behaviour and segments the cache per tenant.

### Tenant Isolation

All named cache policies apply `TenantAwareOutputCachePolicy`, which does two things:

1. **Enables caching for authenticated requests** — overrides the ASP.NET Core default that skips caching when an `Authorization` header is present.
2. **Varies the cache key by `tenant_id` claim** — each tenant gets its own isolated cache partition. A request from tenant A will never be served a cached response generated for tenant B.

This means adding a new cache policy **must** include `.AddPolicy<TenantAwareOutputCachePolicy>()` to remain safe in a multi-tenant deployment.

### Instance Name

All DragonFly keys are prefixed with `ApiTemplate:OutputCache:` to avoid collisions with other applications sharing the same DragonFly instance.

## How It Works

### Caching a Response

1. A GET request arrives and passes through authentication, authorization, and rate limiting.
2. The Output Cache middleware checks DragonFly for a cached response matching the request.
3. **Cache hit** — the cached response is returned immediately; the controller is not invoked.
4. **Cache miss** — the request continues to the controller, the response is generated, stored in DragonFly with the configured expiration and tag, and returned to the client.

### Cache Invalidation

Controllers invalidate cache after mutations (Create, Update, Delete) using tag-based eviction:

```csharp
await _outputCacheStore.EvictByTagAsync("Categories", ct);
```

This removes **all** cached responses tagged with the specified tag. For example, creating a new category evicts both the category list and all individual category responses.

#### Invalidation Map

| Action                        | Tags Evicted          |
| ----------------------------- | --------------------- |
| Create/Update/Delete Product  | `Products`            |
| Delete Product                | `Products`, `Reviews` |
| Create/Update/Delete Category | `Categories`          |
| Create/Delete Review          | `Reviews`             |

Deleting a product also evicts reviews because product reviews become orphaned.

### Tags

Each policy assigns a tag to its cached responses. Tags group related cache entries so they can be invalidated together. A single `EvictByTagAsync` call removes all entries with that tag across all endpoints and URL variations.

## Infrastructure

### Docker Compose (HA Topology)

The Docker Compose setup provides a master/replica topology with HAProxy for high availability:

```yaml
dragonfly-master:
  image: docker.dragonflydb.io/dragonflydb/dragonfly:v1.27.1
  command: dragonfly --maxmemory 256mb --proactor_threads 2

dragonfly-replica:
  image: docker.dragonflydb.io/dragonflydb/dragonfly:v1.27.1
  command: dragonfly --maxmemory 256mb --proactor_threads 2 --replicaof dragonfly-master 6379

dragonfly-proxy:
  image: haproxy:3.1-alpine
  ports:
    - "6379:6379"
  volumes:
    - ./infrastructure/dragonfly/haproxy.cfg:/usr/local/etc/haproxy/haproxy.cfg:ro
```

The API connects to `dragonfly-proxy:6379`.

### Kubernetes

See [`infrastructure/kubernetes/dragonfly/README.md`](../infrastructure/kubernetes/dragonfly/README.md) for deployment instructions using the DragonFly Kubernetes operator.

### Health Check

DragonFly health is monitored at `/health` alongside PostgreSQL, MongoDB, and Keycloak. The health check is tagged as `cache`.

## Adding a New Cache Policy

1. Add the policy in `ApiServiceCollectionExtensions.cs`:

```csharp
options.AddPolicy("MyEntity", builder => builder
    .Expire(TimeSpan.FromSeconds(30))
    .Tag("MyEntity"));
```

2. Decorate GET endpoints with the policy:

```csharp
[HttpGet]
[OutputCache(PolicyName = "MyEntity")]
public async Task<ActionResult<...>> GetAll(CancellationToken ct) { ... }
```

3. Invalidate after mutations:

```csharp
await _outputCacheStore.EvictByTagAsync("MyEntity", ct);
```

## DragonFly vs Redis vs Valkey

DragonFly is a modern, multi-threaded in-memory datastore that is wire-compatible with Redis. It uses the same protocol, commands, and client libraries (`StackExchange.Redis`) as Redis and Valkey. The `AddStackExchangeRedisOutputCache` method works identically with DragonFly.

Key advantages over Redis/Valkey:
- **Multi-threaded architecture** — better utilization of modern multi-core hardware
- **Lower memory overhead** — uses less memory for the same dataset
- **Kubernetes operator** — native operator with automatic failover and replica promotion

### Cache Policies

Defined in `ApiServiceCollectionExtensions.cs`:

| Policy       | Expiration | Tag          | Used By                                  |
| ------------ | ---------- | ------------ | ---------------------------------------- |
| *(base)*     | No cache   | —            | All endpoints by default                 |
| `Products`   | 30 seconds | `Products`   | `ProductsController` GET endpoints       |
| `Categories` | 60 seconds | `Categories` | `CategoriesController` GET endpoints     |
| `Reviews`    | 30 seconds | `Reviews`    | `ProductReviewsController` GET endpoints |

The base policy disables caching for all endpoints. Only endpoints explicitly decorated with `[OutputCache(PolicyName = "...")]` are cached.

> **Important:** By default ASP.NET Core Output Cache does not cache responses that carry an `Authorization` header. All named policies include `TenantAwareOutputCachePolicy` (see [Tenant Isolation](#tenant-isolation)) which overrides this behaviour and segments the cache per tenant.

### Tenant Isolation

All named cache policies apply `TenantAwareOutputCachePolicy`, which does two things:

1. **Enables caching for authenticated requests** — overrides the ASP.NET Core default that skips caching when an `Authorization` header is present.
2. **Varies the cache key by `tenant_id` claim** — each tenant gets its own isolated cache partition. A request from tenant A will never be served a cached response generated for tenant B.

This means adding a new cache policy **must** include `.AddPolicy<TenantAwareOutputCachePolicy>()` to remain safe in a multi-tenant deployment.

### Instance Name

All DragonFly keys are prefixed with `ApiTemplate:OutputCache:` to avoid collisions with other applications sharing the same DragonFly instance.

## How It Works

### Caching a Response

1. A GET request arrives and passes through authentication, authorization, and rate limiting.
2. The Output Cache middleware checks DragonFly for a cached response matching the request.
3. **Cache hit** — the cached response is returned immediately; the controller is not invoked.
4. **Cache miss** — the request continues to the controller, the response is generated, stored in DragonFly with the configured expiration and tag, and returned to the client.

### Cache Invalidation

Controllers invalidate cache after mutations (Create, Update, Delete) using tag-based eviction:

```csharp
await _outputCacheStore.EvictByTagAsync("Categories", ct);
```

This removes **all** cached responses tagged with the specified tag. For example, creating a new category evicts both the category list and all individual category responses.

#### Invalidation Map

| Action                        | Tags Evicted          |
| ----------------------------- | --------------------- |
| Create/Update/Delete Product  | `Products`            |
| Delete Product                | `Products`, `Reviews` |
| Create/Update/Delete Category | `Categories`          |
| Create/Delete Review          | `Reviews`             |

Deleting a product also evicts reviews because product reviews become orphaned.

### Tags

Each policy assigns a tag to its cached responses. Tags group related cache entries so they can be invalidated together. A single `EvictByTagAsync` call removes all entries with that tag across all endpoints and URL variations.

## Infrastructure

### Docker Compose (HA Topology)

The Docker Compose setup provides a master/replica topology with HAProxy for high availability:

```yaml
dragonfly-master:
  image: docker.dragonflydb.io/dragonflydb/dragonfly:v1.27.1
  command: dragonfly --maxmemory 256mb --proactor_threads 2

dragonfly-replica:
  image: docker.dragonflydb.io/dragonflydb/dragonfly:v1.27.1
  command: dragonfly --maxmemory 256mb --proactor_threads 2 --replicaof dragonfly-master 6379

dragonfly-proxy:
  image: haproxy:3.1-alpine
  ports:
    - "6379:6379"
  volumes:
    - ./infrastructure/dragonfly/haproxy.cfg:/usr/local/etc/haproxy/haproxy.cfg:ro
```

The API connects to `dragonfly-proxy:6379`.

### Kubernetes

See [`infrastructure/kubernetes/dragonfly/README.md`](../infrastructure/kubernetes/dragonfly/README.md) for deployment instructions using the DragonFly Kubernetes operator.

### Health Check

DragonFly health is monitored at `/health` alongside PostgreSQL, MongoDB, and Keycloak. The health check is tagged as `cache`.

## Adding a New Cache Policy

1. Add the policy in `ApiServiceCollectionExtensions.cs`:

```csharp
options.AddPolicy("MyEntity", builder => builder
    .Expire(TimeSpan.FromSeconds(30))
    .Tag("MyEntity"));
```

2. Decorate GET endpoints with the policy:

```csharp
[HttpGet]
[OutputCache(PolicyName = "MyEntity")]
public async Task<ActionResult<...>> GetAll(CancellationToken ct) { ... }
```

3. Invalidate after mutations:

```csharp
await _outputCacheStore.EvictByTagAsync("MyEntity", ct);
```

## DragonFly vs Redis vs Valkey

DragonFly is a modern, multi-threaded in-memory datastore that is wire-compatible with Redis. It uses the same protocol, commands, and client libraries (`StackExchange.Redis`) as Redis and Valkey. The `AddStackExchangeRedisOutputCache` method works identically with DragonFly.

Key advantages over Redis/Valkey:
- **Multi-threaded architecture** — better utilization of modern multi-core hardware
- **Lower memory overhead** — uses less memory for the same dataset
- **Kubernetes operator** — native operator with automatic failover and replica promotion
