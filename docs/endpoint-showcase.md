# Endpoint Showcases

This template includes 7 advanced endpoint patterns beyond standard CRUD. Each demonstrates a real-world pattern you can study, adapt, and reuse.

> All showcase endpoints require a Bearer JWT token, except the Webhook Receiver which uses HMAC signature authentication. See [AUTHENTICATION.md](AUTHENTICATION.md) for obtaining tokens.

---

## Overview

| # | Pattern | Route | HTTP | Purpose |
|---|---------|-------|------|---------|
| 1 | [SSE Streaming](#1-sse-streaming) | `/api/v1/sse/stream` | GET | Real-time server push via Server-Sent Events |
| 2 | [File Upload](#2-file-upload--download) | `/api/v1/files/upload` | POST | Upload files with validation |
| 2 | [File Download](#2-file-upload--download) | `/api/v1/files/{id}/download` | GET | Download stored files |
| 3 | [Long-Running Jobs](#3-long-running-jobs-async-request-reply) | `/api/v1/jobs` | POST/GET | Async request-reply with 202 Accepted |
| 4 | [Batch Operations](#4-batch-operations) | `/api/v1/batch/products` | POST | Bulk create with per-item validation |
| 5 | [Idempotent Endpoint](#5-idempotent-endpoint) | `/api/v1/idempotent` | POST | Retry-safe mutations via Idempotency-Key |
| 6 | [JSON Patch](#6-json-patch-partial-update) | `/api/v1/patch/products/{id}` | PATCH | RFC 6902 partial updates |
| 7 | [Webhook Receiver](#7-webhook-receiver) | `/api/v1/webhooks` | POST | Inbound webhooks with HMAC signature verification |

---

## 1. SSE Streaming

**Server-Sent Events** allow the server to push data to the client over a persistent HTTP connection. Unlike WebSockets, SSE is unidirectional (server → client) and uses standard HTTP.

### When to use
- Live notifications, progress updates, real-time feeds
- AI text generation (token-by-token streaming)
- Dashboard metrics, log tailing

### Endpoint

```
GET /api/v1/sse/stream?count=5
Permission: Examples.Read
```

### Request

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | int (1-100) | 5 | Number of events to stream |

### Response

Content-Type: `text/event-stream`

Each event follows the SSE wire format:

```
data: {"sequence":1,"message":"Event 1 of 5","timestampUtc":"2026-03-18T10:00:00Z"}

data: {"sequence":2,"message":"Event 2 of 5","timestampUtc":"2026-03-18T10:00:00Z"}

data: {"sequence":3,"message":"Event 3 of 5","timestampUtc":"2026-03-18T10:00:01Z"}
```

Each `data:` line is followed by two newlines (`\n\n`) per the SSE specification.

### Example (curl)

```bash
curl -N -H "Authorization: Bearer <token>" \
  http://localhost:5000/api/v1/sse/stream?count=3
```

### How it works

```
Client                          Server
  │                               │
  │── GET /sse/stream?count=3 ──→ │
  │                               │  Content-Type: text/event-stream
  │←── data: {sequence:1} ──────  │  (500ms delay between events)
  │←── data: {sequence:2} ──────  │
  │←── data: {sequence:3} ──────  │
  │                               │  Connection closes after last event
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/SseController.cs` | Sets SSE headers, iterates `IAsyncEnumerable`, writes `data:` lines via `StreamWriter` |
| Application | `Features/Examples/Handlers/SseStreamHandler.cs` | Returns `IAsyncEnumerable<SseNotificationItem>` via MediatR |

### Key implementation details
- The handler returns `IAsyncEnumerable<SseNotificationItem>` — the controller streams items as they arrive
- `Response.Headers.CacheControl = "no-cache"` prevents proxy buffering
- `Response.Headers.Connection = "keep-alive"` keeps the connection open
- `CancellationToken` is respected — if the client disconnects, the stream stops
- .NET 10 has native `TypedResults.ServerSentEvents()` for Minimal APIs; this controller-based approach works with both Minimal and MVC

---

## 2. File Upload & Download

Upload files with type/size validation and download them by ID.

### When to use
- Document management, image galleries, CSV imports
- Any API that handles user-uploaded content

### Endpoints

**Upload:**
```
POST /api/v1/files/upload
Content-Type: multipart/form-data
Permission: Examples.Upload
Max size: 10 MB
```

**Download:**
```
GET /api/v1/files/{id}/download
Permission: Examples.Download
```

### Upload Request

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file (IFormFile) | Yes | The file to upload |
| `description` | string | No | Optional description |

### Upload Response (201 Created)

```json
{
  "id": "a1b2c3d4-...",
  "originalFileName": "report.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 245760,
  "description": "Q1 sales report",
  "createdAtUtc": "2026-03-18T10:00:00Z"
}
```

### Download Response

Returns the file with correct `Content-Type` and `Content-Disposition: attachment; filename="report.pdf"` headers.

### Validation

| Rule | Error Code | HTTP Status |
|------|-----------|-------------|
| Extension not in allowed list | `EXA-0400-FILE` | 400 |
| File exceeds 10 MB | `EXA-0400-SIZE` | 400 |
| File not found | `EXA-0404-FILE` | 404 |

Allowed extensions (configurable via `FileStorage:AllowedExtensions`): `.jpg`, `.png`, `.gif`, `.pdf`, `.csv`, `.txt`

### Example (curl)

```bash
# Upload
curl -X POST -H "Authorization: Bearer <token>" \
  -F "file=@report.pdf" \
  -F "description=Q1 sales report" \
  http://localhost:5000/api/v1/files/upload

# Download
curl -H "Authorization: Bearer <token>" \
  -o downloaded.pdf \
  http://localhost:5000/api/v1/files/a1b2c3d4-.../download
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/FilesController.cs` | Accepts `IFormFile`, dispatches to MediatR |
| Api | `Requests/FileUploadRequest.cs` | Binds multipart form data |
| Application | `Features/Examples/Handlers/UploadFileHandler.cs` | Validates extension/size, saves via `IFileStorageService`, persists `StoredFile` entity |
| Application | `Features/Examples/Handlers/DownloadFileHandler.cs` | Loads entity, opens file stream |
| Application | `Common/Contracts/IFileStorageService.cs` | Storage abstraction (save, read, delete) |
| Application | `Common/Options/FileStorageOptions.cs` | Configurable limits and allowed extensions |
| Infrastructure | `FileStorage/LocalFileStorageService.cs` | Local filesystem implementation |
| Domain | `Entities/StoredFile.cs` | Entity: file metadata + storage path |

### Configuration (appsettings.json)

```json
{
  "FileStorage": {
    "BasePath": "/var/data/uploads",
    "MaxFileSizeBytes": 10485760,
    "AllowedExtensions": [".jpg", ".png", ".gif", ".pdf", ".csv", ".txt"]
  }
}
```

### Key implementation details
- Files are stored in `{BasePath}/{TenantId}/{guid}{extension}` — tenant-isolated
- If entity creation fails after file save, the file is cleaned up (rollback)
- The `StoredFile` entity implements `IAuditableTenantEntity` (multi-tenant, soft-deletable, audited)

---

## 3. Long-Running Jobs (Async Request-Reply)

Submit a job for background processing. The server returns `202 Accepted` with a `Location` header pointing to the status endpoint. The client polls for completion.

### When to use
- Report generation, data exports, batch imports
- Any operation that takes longer than a typical HTTP timeout
- Operations that should survive client disconnection

### Endpoints

**Submit:**
```
POST /api/v1/jobs
Permission: Examples.Execute
Returns: 202 Accepted + Location header
```

**Check status:**
```
GET /api/v1/jobs/{id}
Permission: Examples.Read
```

### Submit Request

```json
{
  "jobType": "report-generation",
  "parameters": "{\"format\": \"pdf\", \"dateRange\": \"2026-Q1\"}"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `jobType` | string | Yes | Max 100 chars |
| `parameters` | string | No | Free-form (JSON recommended) |

### Submit Response (202 Accepted)

```
Location: /api/v1/jobs/d5e6f7a8-...

{
  "id": "d5e6f7a8-...",
  "jobType": "report-generation",
  "status": "Pending",
  "progressPercent": 0,
  "submittedAtUtc": "2026-03-18T10:00:00Z",
  ...
}
```

### Status Response (200 OK)

```json
{
  "id": "d5e6f7a8-...",
  "jobType": "report-generation",
  "status": "Completed",
  "progressPercent": 100,
  "parameters": "{\"format\": \"pdf\", \"dateRange\": \"2026-Q1\"}",
  "resultPayload": "{\"summary\": \"Job completed successfully\"}",
  "errorMessage": null,
  "submittedAtUtc": "2026-03-18T10:00:00Z",
  "startedAtUtc": "2026-03-18T10:00:01Z",
  "completedAtUtc": "2026-03-18T10:00:03Z"
}
```

### Job Status Lifecycle

```
Pending → Processing → Completed
                     → Failed
```

| Status | Description |
|--------|-------------|
| `Pending` | Job created, waiting to be picked up |
| `Processing` | Background service is executing the job |
| `Completed` | Job finished successfully, `resultPayload` contains output |
| `Failed` | Job failed, `errorMessage` contains the error |

### Example (curl)

```bash
# Submit
RESPONSE=$(curl -s -D- -X POST -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"jobType": "report-generation"}' \
  http://localhost:5000/api/v1/jobs)

# Extract Location header and poll
LOCATION=$(echo "$RESPONSE" | grep -i location | awk '{print $2}' | tr -d '\r')
curl -H "Authorization: Bearer <token>" "http://localhost:5000$LOCATION"
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/JobsController.cs` | Returns `AcceptedAtAction` with Location header |
| Application | `Features/Examples/Handlers/JobRequestHandlers.cs` | Creates `JobExecution` (Pending), enqueues to `IJobQueue` |
| Application | `Common/BackgroundJobs/IJobQueue.cs` | Queue abstraction |
| Infrastructure | `BackgroundJobs/Services/ChannelJobQueue.cs` | `Channel<Guid>` bounded queue (same pattern as email queue) |
| Infrastructure | `BackgroundJobs/Services/JobProcessingBackgroundService.cs` | `BackgroundService` — reads from queue, updates progress, marks completed/failed |
| Domain | `Entities/JobExecution.cs` | Entity with status lifecycle methods |
| Domain | `Enums/JobStatus.cs` | Pending, Processing, Completed, Failed |

### Key implementation details
- Background processing uses `System.Threading.Channels` — same pattern as the email queue
- `JobProcessingBackgroundService` creates a DI scope per job (via `IServiceScopeFactory`)
- Progress updates are persisted to DB — clients see real-time progress via polling
- The `JobExecution` entity uses `TimeProvider` for testable timestamps
- Job is enqueued **after** the DB commit — ensures the entity exists when the worker picks it up

---

## 4. Batch Operations

Create, update, and delete multiple products or categories in a single request with per-item validation. All-or-nothing semantics: if any item fails validation, nothing is persisted.

### When to use
- Bulk imports (CSV → API), bulk creation/updates/deletes
- Reducing round-trips for multi-item operations

### Endpoints

```
POST   /api/v1/products      — Batch create products  (Permission: Products.Create)
PUT    /api/v1/products      — Batch update products  (Permission: Products.Update)
DELETE /api/v1/products      — Batch delete products  (Permission: Products.Delete)

POST   /api/v1/categories    — Batch create categories (Permission: Categories.Create)
PUT    /api/v1/categories    — Batch update categories (Permission: Categories.Update)
DELETE /api/v1/categories    — Batch delete categories (Permission: Categories.Delete)
```

### Request

```json
{
  "items": [
    { "name": "Wireless Mouse", "description": "Ergonomic", "price": 29.99 },
    { "name": "Keyboard", "price": 79.99 },
    { "name": "", "price": -5 }
  ]
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `items` | array | Yes | 1-100 items |
| `items[].name` | string | Yes | Max 200 chars |
| `items[].description` | string | No | |
| `items[].price` | decimal | Yes | Must be positive |

### Response — All Valid (200 OK)

```json
{
  "failures": [],
  "successCount": 2,
  "failureCount": 0
}
```

### Response — Some Invalid (422 Unprocessable Entity)

```json
{
  "failures": [
    {
      "index": 0,
      "id": null,
      "errors": [
        "Product name is required.",
        "Price must be greater than zero."
      ]
    },
    {
      "index": 1,
      "id": "c38a5227-5324-4d8c-b1d7-6245d1ca820d",
      "errors": [
        "Product 'c38a5227-5324-4d8c-b1d7-6245d1ca820d' not found."
      ]
    }
  ],
  "successCount": 0,
  "failureCount": 2
}
```

> When ANY item fails validation, NO items are persisted (all-or-nothing).

### Example (curl)

```bash
curl -X POST -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"items": [{"name": "Mouse", "price": 30}, {"name": "Keyboard", "price": 80}]}' \
  http://localhost:5000/api/v1/products
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/ProductsController.cs` | Batch create/update/delete for products |
| Api | `Controllers/V1/CategoriesController.cs` | Batch create/update/delete for categories |
| Application | `Features/Product/Commands/CreateProductsCommand.cs` | Validates each item, creates all in `ExecuteInTransactionAsync` if all valid |
| Application | `Features/Product/DTOs/CreateProductsRequest.cs` | Request with items collection |
| Application | `Common/DTOs/BatchResponse.cs` | Standard batch output (`failures`, `successCount`, `failureCount`) |
| Application | `Common/CQRS/BatchFailureContext.cs` + `Common/CQRS/Rules/*` | Reusable validation and existence-checking orchestration |

### Key implementation details
- Individual item validation via FluentValidation — each item gets its own error list
- All-or-nothing: if validation fails for any item, the entire batch is rejected (no partial writes)
- All valid items are persisted within a single `ExecuteInTransactionAsync` call
- Bulk reference validation (categories, product data) in single DB queries
- Reuses existing domain entities and repositories

---

## 5. Idempotent Endpoint

Guarantees that retrying the same request produces the same result without duplicating side effects. The client sends an `Idempotency-Key` header, and the server caches the response for subsequent requests with the same key.

### When to use
- Payment processing (Stripe, PayPal use this pattern)
- Order creation — retry without duplicates
- Any mutation where duplicates would be harmful
- Mobile apps with unreliable connections

### Endpoint

```
POST /api/v1/idempotent
Permission: Examples.Create
Header: Idempotency-Key: <unique-key>
```

### Request

```json
{
  "name": "My Resource",
  "description": "Optional description"
}
```

### Response (201 Created)

```json
{
  "id": "f1a2b3c4-...",
  "name": "My Resource",
  "description": "Optional description",
  "createdAtUtc": "2026-03-18T10:00:00Z"
}
```

### How idempotency works

```
Request 1: POST + Idempotency-Key: "abc-123"
  → Server executes operation, creates resource, caches response
  → Returns 201 Created { id: "xyz" }

Request 2: POST + Idempotency-Key: "abc-123"  (retry / duplicate)
  → Server finds cached response for key "abc-123"
  → Returns 201 Created { id: "xyz" }  (same response, no new resource created)

Request 3: POST + Idempotency-Key: "def-456"  (different key)
  → Server executes operation, creates NEW resource
  → Returns 201 Created { id: "uvw" }
```

### Idempotency-Key header rules

| Rule | Behavior |
|------|----------|
| Key present, first time | Execute action, cache 2xx response |
| Key present, already seen | Return cached response (no execution) |
| Key absent | Execute action normally (no caching) |
| Error response (4xx/5xx) | NOT cached — client can retry with same key |
| Key too long (>100 chars) | 400 Bad Request |
| TTL | 24 hours (configurable) |

### Example (curl)

```bash
# First request — creates resource
curl -X POST -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: order-$(uuidgen)" \
  -d '{"name": "My Order"}' \
  http://localhost:5000/api/v1/idempotent

# Retry with same key — returns cached response (no duplicate)
curl -X POST -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: order-same-key" \
  -d '{"name": "My Order"}' \
  http://localhost:5000/api/v1/idempotent
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/IdempotentController.cs` | `[Idempotent]` attribute on the POST action |
| Api | `Filters/IdempotentAttribute.cs` | Marker attribute (`[AttributeUsage(Method)]`) |
| Api | `Filters/IdempotencyActionFilter.cs` | `IAsyncActionFilter` — checks key, replays or caches response |
| Application | `Common/Contracts/IIdempotencyStore.cs` | Interface for key-value cache |
| Infrastructure | `Idempotency/DistributedCacheIdempotencyStore.cs` | Implementation backed by `IConnectionMultiplexer`/Redis (DragonFly in prod, in-memory fallback) |

### How to apply to your own endpoints

Add `[Idempotent]` to any controller action:

```csharp
[HttpPost]
[Idempotent]  // ← This is all you need
public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request, CancellationToken ct)
{
    // Your normal logic — the filter handles idempotency transparently
}
```

### Key implementation details
- Cross-cutting concern implemented as a global `IAsyncActionFilter` — works on any endpoint marked with `[Idempotent]`
- Storage uses `IConnectionMultiplexer`/Redis (DragonFly in production, in-memory fallback) — no DB table needed
- Only 2xx responses are cached — error responses can be retried
- Concurrent requests with the same key are serialized via distributed locking
- Cache key format: `idempotency:{key}`

---

## 6. JSON Patch (Partial Update)

Update specific fields of a resource without sending the entire object. Uses the RFC 6902 JSON Patch format.

### When to use
- Updating a single field without resending the full object
- Complex field-level operations (add, remove, replace, copy, move)
- Mobile apps minimizing payload size

### Endpoint

```
PATCH /api/v1/patch/products/{id}
Content-Type: application/json-patch+json
Permission: Examples.Update
```

### Request Body

JSON Patch is an array of operations:

```json
[
  { "op": "replace", "path": "/name", "value": "Updated Product Name" },
  { "op": "replace", "path": "/price", "value": 49.99 },
  { "op": "remove", "path": "/description" }
]
```

### Supported Operations

| Operation | Description | Example |
|-----------|-------------|---------|
| `replace` | Change a field's value | `{"op": "replace", "path": "/name", "value": "New Name"}` |
| `add` | Set a field (or add to array) | `{"op": "add", "path": "/description", "value": "New desc"}` |
| `remove` | Clear a field (set to null) | `{"op": "remove", "path": "/description"}` |
| `copy` | Copy value from one field to another | `{"op": "copy", "from": "/name", "path": "/description"}` |
| `move` | Move value from one field to another | `{"op": "move", "from": "/description", "path": "/name"}` |
| `test` | Assert a field has a value (fails if not) | `{"op": "test", "path": "/price", "value": 50}` |

### Patchable Fields

| Field | Type | Validation |
|-------|------|------------|
| `/name` | string | Required, max 200 chars |
| `/description` | string? | Max 1000 chars; required if price > 1000 |
| `/price` | decimal | Must be positive |
| `/categoryId` | guid? | Optional |

### Response (200 OK)

Returns the full updated product:

```json
{
  "id": "aaa-...",
  "name": "Updated Product Name",
  "description": null,
  "price": 49.99,
  "categoryId": null,
  "createdAtUtc": "2026-03-18T09:00:00Z",
  "productDataIds": []
}
```

### Example (curl)

```bash
# Change only the name
curl -X PATCH -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json-patch+json" \
  -d '[{"op": "replace", "path": "/name", "value": "Patched Name"}]' \
  http://localhost:5000/api/v1/patch/products/aaa-...
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/PatchController.cs` | Accepts `JsonPatchDocument<PatchableProductDto>`, passes apply delegate to handler |
| Application | `Features/Examples/DTOs/PatchableProductDto.cs` | Mutable class with validation attributes |
| Application | `Features/Examples/Handlers/PatchRequestHandlers.cs` | Loads product, applies patch, validates result, updates entity |

### Key implementation details
- Uses `SystemTextJsonPatch` library (System.Text.Json-native, no Newtonsoft dependency)
- The handler receives an `Action<PatchableProductDto>` delegate — decouples the patch document format from business logic
- Post-patch validation ensures the patched entity is valid (e.g., name not empty, price positive)
- Content-Type must be `application/json-patch+json`

---

## 7. Webhook Receiver

Receive inbound webhooks from external systems with HMAC-SHA256 signature verification and async background processing.

### When to use
- Payment gateway callbacks (Stripe, PayPal, Shopify)
- CI/CD notifications (GitHub, GitLab)
- Third-party integration events

### Endpoint

```
POST /api/v1/webhooks
Authentication: HMAC signature (no JWT required)
Max payload: 1 MB
```

### Request

**Required Headers:**

| Header | Description |
|--------|-------------|
| `X-Webhook-Signature` | HMAC-SHA256 hex digest of `{timestamp}.{body}` |
| `X-Webhook-Timestamp` | Unix timestamp (seconds) when the webhook was sent |

**Body:**

```json
{
  "eventType": "payment.completed",
  "eventId": "evt_abc123",
  "data": {
    "orderId": "ord_xyz",
    "amount": 99.99,
    "currency": "USD"
  }
}
```

### Response

| Scenario | HTTP Status | Description |
|----------|-------------|-------------|
| Valid signature | 200 OK | Payload enqueued for processing |
| Invalid/missing signature | 401 Unauthorized | ProblemDetails with `EXA-0401-WEBHOOK` error code |
| Missing headers | 401 Unauthorized | ProblemDetails with `EXA-0401-WEBHOOK-HDR` error code |
| Expired timestamp | 401 Unauthorized | ProblemDetails with `EXA-0401-WEBHOOK` (timestamp older than tolerance, default 5 minutes) |

### Signature Verification

The sender computes:

```
signature = HMAC-SHA256(
    key: shared_secret,
    message: "{timestamp}.{raw_body}"
)
```

The server recomputes the same HMAC and compares using constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks.

### Example (curl)

```bash
SECRET="your-webhook-secret-min-16-chars"
TIMESTAMP=$(date +%s)
BODY='{"eventType":"payment.completed","eventId":"evt_123","data":{"amount":99.99}}'
SIGNATURE=$(echo -n "${TIMESTAMP}.${BODY}" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')

curl -X POST \
  -H "Content-Type: application/json" \
  -H "X-Webhook-Signature: $SIGNATURE" \
  -H "X-Webhook-Timestamp: $TIMESTAMP" \
  -d "$BODY" \
  http://localhost:5000/api/v1/webhooks
```

### Architecture

| Layer | File | Role |
|-------|------|------|
| Api | `Controllers/V1/WebhooksController.cs` | `[AllowAnonymous]` + `[ValidateWebhookSignature]`, enqueues to background queue |
| Api | `Filters/ValidateWebhookSignatureAttribute.cs` | Marker attribute |
| Api | `Filters/WebhookSignatureResourceFilter.cs` | `IAsyncResourceFilter` — reads raw body, validates HMAC |
| Application | `Common/Contracts/IWebhookPayloadValidator.cs` | Signature validation interface |
| Application | `Common/Options/WebhookOptions.cs` | Secret + timestamp tolerance |
| Application | `Common/BackgroundJobs/IWebhookProcessingQueue.cs` | Queue interface |
| Infrastructure | `Webhooks/HmacWebhookPayloadValidator.cs` | HMAC-SHA256 implementation |
| Infrastructure | `Webhooks/ChannelWebhookQueue.cs` | `Channel<WebhookPayload>` bounded queue |
| Infrastructure | `Webhooks/WebhookProcessingBackgroundService.cs` | Background consumer |

### Configuration (appsettings.json)

```json
{
  "Webhook": {
    "Secret": "your-secret-key-minimum-16-characters",
    "TimestampToleranceSeconds": 300
  }
}
```

### Key implementation details
- `[AllowAnonymous]` — webhooks authenticate via HMAC, not JWT
- Signature validation happens in a resource filter before the controller runs
- Constant-time comparison prevents timing attacks
- Timestamp tolerance (default 5 min) prevents replay attacks
- Payloads are processed asynchronously via `Channel<WebhookPayload>` — the controller returns 200 immediately
- Request size limited to 1 MB to prevent abuse

---

## Database Tables

These showcase endpoints add two tables:

### `ExampleFiles`

Stores file upload metadata. The actual file bytes are on the local filesystem.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid | PK |
| OriginalFileName | varchar(255) | |
| StoragePath | varchar(500) | Filesystem path |
| ContentType | varchar(100) | MIME type |
| SizeBytes | bigint | |
| Description | varchar(1000) | Optional |
| TenantId | uuid | FK → Tenants |
| *audit + soft-delete columns* | | Via `ConfigureTenantAuditable()` |

### `JobExecutions`

Tracks background job lifecycle.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid | PK |
| JobType | varchar(100) | |
| Status | varchar(20) | Pending / Processing / Completed / Failed |
| ProgressPercent | int | 0-100 |
| Parameters | text | JSON input |
| ResultPayload | text | JSON output (on completion) |
| ErrorMessage | text | On failure |
| SubmittedAtUtc | timestamptz | |
| StartedAtUtc | timestamptz | Nullable |
| CompletedAtUtc | timestamptz | Nullable |
| TenantId | uuid | FK → Tenants |
| *audit + soft-delete columns* | | Via `ConfigureTenantAuditable()` |

---

## Permissions

All showcase endpoints use permissions from `Permission.Examples`:

| Permission | Used by |
|------------|---------|
| `Examples.Read` | SSE stream, Job status |
| `Examples.Create` | Batch create, Idempotent create |
| `Examples.Update` | JSON Patch |
| `Examples.Execute` | Submit job |
| `Examples.Upload` | File upload |
| `Examples.Download` | File download |

---

## Error Codes

| Code | Meaning |
|------|---------|
| `EXA-0404-FILE` | File not found |
| `EXA-0400-FILE` | Invalid file type (extension not allowed) |
| `EXA-0400-SIZE` | File too large |
| `EXA-0400-PATCH` | Invalid patch document |
| `EXA-0401-WEBHOOK` | Invalid webhook signature |
| `EXA-0401-WEBHOOK-HDR` | Missing webhook headers |

---

## Related Documentation

- [REST Endpoint Guide](rest-endpoint.md) — How to create a standard CRUD endpoint
- [GraphQL Endpoint Guide](graphql-endpoint.md) — Queries, mutations, and DataLoaders
- [Authentication](AUTHENTICATION.md) — JWT tokens and BFF flow
- [Caching](CACHING.md) — Output caching and DragonFly
- [Testing](testing.md) — Integration and unit test patterns
