# File Upload Saga – Content-Addressed Storage

End-to-end reference for the `FileStorage` module after the saga refactor. Covers the atomicity problem that was solved, the architecture, the API, configuration, and how to extend the blob-store layer with a second backend (S3, Azure Blob).

---

## Why this module exists

The legacy single-step upload wrote bytes to disk **before** opening the DB transaction. Any process kill between disk-write and commit left an orphan file on disk with no DB row referencing it. Catch-based compensation only ran on caught exceptions — hard kills leaked storage.

The refactor splits the upload into two atomic phases orchestrated by a Wolverine EF Core saga, and switches the on-disk layout to **content-addressed storage (CAS)**: every blob lives at `{backend}/{tenant}/{sha256[:2]}/{sha256}`. Same bytes within a tenant deduplicate naturally; reference counting is the `StoredFile` row.

---

## Lifecycle

```
POST /api/v1/files/staging (multipart stream)
    │
    ▼
BeginUploadEndpointCommand  ── validate (ext, size)
    │                          └─ IBlobStore.WriteStagingAsync → (stagingPath, sha256, size)
    │
    ▼
IMessageBus.PublishAsync(BeginUploadCommand)
    │
    ▼
FileUploadSaga.Start(...)           Status = Staged
    └─ schedule TimeoutUploadCommand (DeliverIn StagingTtlMinutes)
    │
    ▼ response → { uploadToken, sha256, sizeBytes }

                              ┌──────────── happy path ───────────┐
POST /api/v1/files/commit     │                                   │
    │                         ▼                                   ▼
    ▼                  CommitUploadCommand            TimeoutUploadCommand
CommitUploadEndpointCommand        │                       │
    │                         ▼                       ▼
    ▼                  saga.Handle(commit)        saga.Handle(timeout)
InvokeAsync<UploadCommittedReply>  │                       │
                              ▼                       ▼
                       IBlobStore.Promote        IBlobStore.DeleteStaging
                       insert StoredFile         Status = Failed
                       cascade: StoredFileCreatedNotification
                       Status = Committed

DELETE /api/v1/files/{id}
    └─ soft-delete StoredFile + cascade MaybeDeleteBlobCommand
            └─ MaybeDeleteBlobHandler
                  refcount = COUNT(*) WHERE Sha256=@sha AND TenantId=@t AND !IsDeleted
                  if refcount == 0: IBlobStore.DeleteAsync
```

The saga state machine is strict: every `Handle` first checks the expected status. Redelivery (Wolverine inbox replay) is idempotent — committing an already-committed saga returns the existing reply; timeout after commit no-ops.

### Per-tenant isolation (important)

Blobs live under `blobs/{tenantId}/{sha[:2]}/{sha}`. Deduplication happens **within** a tenant only. Cross-tenant dedup would create a timing-attack primitive (tenant B uploading identical bytes could detect that tenant A already stores them). This is a deliberate privacy trade-off: a few percent of storage savings lost to keep blobs isolated.

---

## Components

| Concern | Type | Location |
|---------|------|----------|
| Saga state + handlers | `FileUploadSaga : Wolverine.Saga` | `Domain/Sagas/FileUploadSaga.cs` |
| Saga commands / events | `BeginUploadCommand`, `CommitUploadCommand`, `TimeoutUploadCommand`, `MaybeDeleteBlobCommand`, `StoredFileCreatedNotification`, `UploadCommittedReply` | `Domain/Sagas/FileUploadSagaMessages.cs` |
| Blob abstraction | `IBlobStore`, `IBlobStoreFactory`, `StagingResult` | `Domain/Storage/` |
| Local blob impl | `LocalBlobStore`, `BlobStoreFactory`, `KeyedBlobStore` | `Services/` |
| Commit/begin handlers | `BeginUploadEndpointCommand`, `CommitUploadEndpointCommand` | `Features/Staging`, `Features/Commit` |
| V1 facade | `UploadFileCommand` (chains begin+commit) | `Features/Upload/UploadFileCommand.cs` |
| Delete flow | `DeleteFileCommand`, `MaybeDeleteBlobHandler` | `Features/Delete/` |
| Reaper | `IOrphanBlobSweepService`, `OrphanBlobSweepService`, `SweepOrphanBlobsHandler` | `Services/OrphanBlobSweepService.cs`, `Features/Sweep/` |
| Recurring job | `OrphanBlobRecurringJob` (TickerQ in BackgroundJobs) | `src/Modules/BackgroundJobs/TickerQ/Jobs/OrphanBlobRecurringJob.cs` |
| Refcount spec | `ActiveStoredFilesBySha256AndTenantSpecification` | `Domain/` |
| Persistence | `StoredFileConfiguration`, `FileUploadSagaConfiguration`, EF Migrations | `Persistence/` |

---

## Data model

**`stored_files`** (EF schema `file_storage`)

| Column | Type | Notes |
|--------|------|-------|
| `Id` | uuid PK | |
| `TenantId` | uuid | tenancy |
| `OriginalFileName` | varchar(255) | original client-supplied name |
| `Sha256` | char(64) | lower-hex SHA-256 of the bytes |
| `BackendKey` | varchar(32) | blob-store backend (e.g. `local`) |
| `ContentType` | varchar(100) | MIME supplied on commit |
| `SizeBytes` | bigint | server-observed size |
| `Description` | varchar(500) NULL | optional |
| `Audit.*` | | standard auditable mixin |
| `IsDeleted`, `DeletedAtUtc`, `DeletedBy` | | soft-delete columns |

Index: `IX_StoredFiles_Sha256_TenantId` — powers refcount queries.

**`file_upload_sagas`**

| Column | Type | Notes |
|--------|------|-------|
| `Id` | varchar(64) PK | upload token (Guid-N) |
| `TenantId`, `Sha256`, `SizeBytes`, `OriginalFileName`, `StagingPath`, `BackendKey` | | saga payload |
| `Status` | varchar(16) | `Staged`/`Committed`/`Failed` (enum→string) |
| `CreatedAtUtc`, `CommitDeadlineUtc` | timestamptz | TTL bookkeeping |
| `StoredFileId` | uuid NULL | populated on commit |

---

## API reference (v1)

All endpoints require `Permission.Examples.Upload` (or `...Download` for download). Rate/size limits inherited from `FileStorageOptions.MaxFileSizeBytes`.

### `POST /api/v1/files/upload` – Legacy facade

Single-step upload. Internally calls staging + commit in one request. Kept for backward compatibility.

```bash
curl -X POST http://localhost:5000/api/v1/files/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@photo.png" -F "description=Holiday photo"
# → 201 Created
# Location: /api/v1/files/{id}/download
# { "id": "...", "originalFileName": "photo.png", ... }
```

### `POST /api/v1/files/staging` – Two-phase upload, phase 1

```bash
curl -X POST http://localhost:5000/api/v1/files/staging \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@photo.png"
# → 200 OK
# { "uploadToken": "<guid-N>", "sha256": "...", "sizeBytes": 12345 }
```

No `stored_files` row is created yet. The caller has `StagingTtlMinutes` to commit before the saga compensates.

### `POST /api/v1/files/commit` – Two-phase upload, phase 2

```bash
curl -X POST http://localhost:5000/api/v1/files/commit \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{ "uploadToken": "<guid-N>", "contentType": "image/png", "description": "Holiday photo" }'
# → 201 Created
# { "id": "...", "originalFileName": "photo.png", ... }
```

Committing an expired upload returns `410 Gone` / `EXA-0410-UPLOAD`.
Committing an unknown token returns `404 / EXA-0404-UPLOAD`.

### `GET /api/v1/files/{id}/download`

Streams the content-addressed blob resolved via `Sha256 + BackendKey`.

### `DELETE /api/v1/files/{id}`

Soft-deletes the `StoredFile` and cascades `MaybeDeleteBlobCommand`. The physical blob is removed only when every non-soft-deleted row referencing the hash within the tenant is gone.

---

## Configuration (`appsettings.json`)

```jsonc
{
  "FileStorage": {
    "BasePath": "/var/lib/apitemplate/files",   // required
    "StagingPath": null,                          // default: {BasePath}/staging
    "BlobsPath": null,                            // default: {BasePath}/blobs
    "BackendKey": "local",                        // persisted on new rows
    "MaxFileSizeBytes": 10485760,                 // 10 MB
    "AllowedExtensions": [".png", ".jpg", ".pdf"],
    "StagingTtlMinutes": 30,
    "BlobRetentionHours": 24,
    "OrphanReaperCron": "0 0 * * * ?",            // 6-field with seconds
    "OrphanReaperEnabled": true
  },
  "BackgroundJobs": {
    "OrphanBlob": {
      "Enabled": true,
      "Cron": "0 0 * * * ?"
    }
  }
}
```

Options are bound and **validated at startup** (`AddValidatedOptions<FileStorageOptions>`). Misconfiguration fails fast.

---

## How to add a new storage backend

Say we want an S3 backend keyed `"s3"`:

1. Add a new package ref to `FileStorage.csproj` (e.g. `AWSSDK.S3`).
2. Implement `IBlobStore` in `Services/S3BlobStore.cs` using streaming SHA-256 via `IncrementalHash`. Keep the same semantic contract:
   - `WriteStagingAsync` → returns `StagingResult(stagingPath, sha, size)` (path can be an S3 key prefix).
   - `PromoteToCommittedAsync` is **idempotent**: if the target key already exists, compare size and skip.
   - `OpenReadAsync` returns a seekable stream or null.
   - `DeleteAsync` / `DeleteStagingAsync` are idempotent.
   - Run destructive ops through the shared Polly pipeline (`ResiliencePipelineKeys.FileStorageDelete`).
3. Register in `FileStorageModule.RegisterApplicationServices`:
   ```csharp
   services.AddScoped<S3BlobStore>();
   services.AddScoped(sp => new KeyedBlobStore("s3", sp.GetRequiredService<S3BlobStore>()));
   ```
4. Flip `FileStorageOptions:BackendKey = "s3"` for new uploads. Legacy rows keep their original `BackendKey` and continue reading from the `"local"` store — **never mutate an existing row's `BackendKey`** without physically moving the bytes.
5. No changes to the saga, controller, handlers, or tests. The seam is the factory.

---

## Operational runbook

### Find orphan staging files

```sql
-- Sagas still Staged past their deadline
SELECT id, created_at_utc, commit_deadline_utc, staging_path
FROM file_storage.file_upload_sagas
WHERE status = 'Staged' AND commit_deadline_utc < now()
ORDER BY created_at_utc;
```

Disk-side: `ls -lat $StagingPath/`. Anything older than `2 × StagingTtlMinutes` is reaper residue.

### Force a reap manually

Publish a `SweepOrphanBlobsCommand` via any admin tooling, or simply restart the TickerQ scheduler — the recurring job runs on the configured cron.

### Recover from saga stuck in `Staged`

Extremely unlikely after the `TimeoutUploadCommand` schedule — but if the durable scheduler was lost before the timeout persisted:

```sql
UPDATE file_storage.file_upload_sagas
SET status = 'Failed'
WHERE id = '<token>' AND status = 'Staged';
```

Then wait for the orphan reaper to sweep the staging payload.

### Identify zero-refcount blobs

The reaper already does this. Manual SQL for ops:

```sql
SELECT t.tenant_id, f.sha256, count(*) active_refs
FROM file_storage.stored_files f
WHERE NOT f.is_deleted
GROUP BY 1, 2
HAVING count(*) = 0;  -- never true; use LEFT JOIN with filesystem list instead
```

For true cross-check, list every blob in `$BlobsPath/{tenant}/**` and left-join with `active_refs`.

---

## Hash-collision guard

SHA-256 collision probability is negligible, but `LocalBlobStore.PromoteToCommittedAsync` verifies `File.Length == expectedSize` when the target path already exists. Size mismatch throws `InvalidOperationException` instead of silently overwriting. An integrity violation is logged and the staging file is left in place for forensics.

---

## Testing

Unit tests live in `tests/APITemplate.Tests/Unit/FileStorage/`:

- `LocalBlobStoreTests.cs` – streaming hash correctness, atomic/idempotent promote, hash-collision guard, path-traversal rejection.
- `BlobStoreFactoryTests.cs` – keyed lookup + unknown-key failure.
- `MaybeDeleteBlobHandlerTests.cs` – refcount>0 skip vs refcount=0 delete.
- `BeginUploadEndpointCommandHandlerTests.cs` – validation boundaries + staging bus publish.

Integration tests (Docker / Testcontainers) are scheduled under `Integration/Postgres/FileUpload*`. See `docs/testing.md` for the Wolverine durable-runtime harness pattern.

---

## Related guides

- [Wolverine message handling](wolverine-message-handling.md) — saga storage, outbox, cascading messages.
- [EF Migrations](ef-migration.md) — `FileStorage` is migrations-based; other modules still use `EnsureCreatedAndTablesAsync`.
- [Transactions](transactions.md) — how the saga's DbContext enlists with the Wolverine outbox.
- [Observability](observability.md) — log fields `uploadToken`, `tenantId`, `sha256`; metric names `filestorage.upload.*`.
