# Rate Limiting

## Overview

The application implements robust **Rate Limiting** using the native ASP.NET Core middleware (`Microsoft.AspNetCore.RateLimiting`). This protects the API from denial-of-service (DoS) attacks, brute-force attempts, and resource exhaustion by limiting the number of requests a client can make within a given timeframe.

## How It Works

Rate limiting is enforced early in the request pipeline, before most other middleware (except for error handling and logging).

### 1. Partitioning (Client Identification)

Limits are applied per **Partition Key** to ensure that one misbehaving client does not impact others. The key is resolved in the following order:

1. **JWT Username** (`sub` or `preferred_username` claim) — for authenticated requests.
2. **Remote IP Address** — for anonymous requests.
3. **`"anonymous"`** — a global fallback key if neither is available.

### 2. Policies

The application supports three distinct rate-limiting strategies, configurable via `appsettings.json`.

#### A. Global Limiter (Token Bucket)
Applied to **every request** entering the API.
- **Algorithm:** Token Bucket.
- **Behavior:** The "bucket" starts with a full set of tokens. Each request consumes one token. Tokens are replenished at a fixed rate over time.
- **Use Case:** Provides a baseline "speed limit" for the entire API to prevent overall system overload.

#### B. Fixed Window Policy
Opt-in via `[EnableRateLimiting(RateLimitPolicies.Fixed)]`.
- **Algorithm:** Fixed Window.
- **Behavior:** Allows a set number of requests within a static time window (e.g., 100 requests every 1 minute). The counter resets abruptly at the end of the window.
- **Use Case:** Standard protection for specific resource-intensive endpoints.

#### C. Sliding Window Policy
Opt-in via `[EnableRateLimiting(RateLimitPolicies.Sliding)]`.
- **Algorithm:** Sliding Window.
- **Behavior:** Similar to fixed window but divides the window into segments. The "limit" is checked across the most recent segments, providing a smoother transition and preventing bursts at window boundaries.
- **Use Case:** Critical endpoints requiring smoother traffic shaping (e.g., Auth, Webhooks).

## Configuration

Settings are located in the `RateLimiting` section of `appsettings.json`.

```json
{
  "RateLimiting": {
    "Global": { 
      "PermitLimit": 1000, 
      "WindowMinutes": 1, 
      "QueueLimit": 0 
    },
    "Fixed": { 
      "PermitLimit": 100, 
      "WindowMinutes": 1, 
      "QueueLimit": 0 
    },
    "Sliding": { 
      "PermitLimit": 100, 
      "WindowMinutes": 1, 
      "SegmentsPerWindow": 4, 
      "QueueLimit": 0 
    }
  }
}
```

| Setting             | Default | Description                                                                 |
| ------------------- | ------- | --------------------------------------------------------------------------- |
| `PermitLimit`       | Varies  | The maximum number of requests allowed in the window/bucket.                |
| `WindowMinutes`     | `1`     | The duration of the window or the replenishment period.                      |
| `QueueLimit`        | `0`     | Number of requests to queue when the limit is reached (default: reject immediately).|
| `SegmentsPerWindow` | `4`     | (Sliding only) Number of segments for the sliding window precision.          |
| `TokensPerPeriod`   | `1000`  | (Global only) Number of tokens added to the bucket every `WindowMinutes`.    |

## Standard Headers & 429 Response

When a limit is exceeded, the API returns **HTTP 429 Too Many Requests**.

### Response Headers (RFC Compliant)

The application emits standard headers to help clients implement backoff strategies:

| Header                  | Description                                                                 |
| ----------------------- | --------------------------------------------------------------------------- |
| `Retry-After`           | Seconds to wait before retrying (as per RFC 7231).                          |
| `RateLimit-Limit`       | The maximum permitted requests in the current window.                       |
| `RateLimit-Remaining`   | Remaining requests available in the current window.                         |
| `RateLimit-Reset`       | Seconds remaining until the current window resets.                          |
| `RateLimit-Policy`      | The name of the policy that triggered the rejection (`global`, `fixed`, etc). |

### Error Payload

The response body follows the project's standard error format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Rate limit exceeded",
  "status": 429,
  "detail": "Quota exceeded. Please try again later.",
  "extensions": {
    "code": "GEN-0429"
  }
}
```

## Usage in Code

### Opting-in to a Policy

Decorate your controller or action with the `[EnableRateLimiting]` attribute:

```csharp
[ApiController]
[Route("api/v1/critical-resource")]
[EnableRateLimiting(RateLimitPolicies.Fixed)] // Use the Fixed window policy
public class MyController : ControllerBase { ... }
```

### Disabling Rate Limiting

If an endpoint must be exempt from **named policies** (the Global limiter still applies unless explicitly bypassed in the middleware configuration), use `[DisableRateLimiting]`:

```csharp
[HttpGet("ping")]
[DisableRateLimiting]
public IActionResult Ping() => Ok();
```

## Implementation Details

- **Middleware:** `app.UseRateLimiter()` is registered in `ApplicationBuilderExtensions.cs`.
- **Service Registration:** `services.AddRateLimiting(configuration)` is registered in `RateLimitingServiceCollectionExtensions.cs`.
- **Validation:** Options are validated at startup. `PermitLimit` must be greater than 0.
- **Storage:** The current implementation uses in-memory partitioning. However, the system is designed to share the same `RedisOptions` as other infrastructure, making it ready for a **DragonFly**-backed distributed rate limiter if horizontal scaling requires global quota enforcement.
