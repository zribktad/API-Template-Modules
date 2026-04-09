# Wolverine — Message Handling

An overview of how Wolverine processes messages in this project: when each mechanism is used, how failures are handled, and where each setting lives.

---

## 1. Two Ways Wolverine Processes Messages

### A) `InvokeAsync` — Inline (Synchronous)

The controller calls the handler **directly**, waits for the result, and returns it as the HTTP response.

```
HTTP Request → Controller → bus.InvokeAsync(command) → Handler → HTTP Response
```

**Used for**: all command handlers called from a controller (`CreateUser`, `UpdateUser`, `DeleteUser`, …).

**On exception**: the exception propagates directly to the controller → HTTP 500.  
Of all error handling policies, only **`Retry` and `RetryWithCooldown`** are applied automatically.  
`ScheduleRetry`, `MoveToErrorQueue`, `Requeue`, and other actions are **not triggered** during `InvokeAsync`.

> **Source**: [Wolverine docs — Error Handling](https://wolverinefx.net/guide/handlers/error-handling.html)  
> *"When using `IMessageBus.InvokeAsync()` to execute a message inline, only the 'Retry' and 'Retry With Cooldown' error policies are applied to the execution automatically."*

---

### B) Durable Outbox Queue — Asynchronous

The handler writes an event to the PostgreSQL outbox table. A Wolverine background worker picks it up and runs the handler **outside the HTTP request**.

```
HTTP Request → Controller → InvokeAsync(command) → Handler
                                                        │
                                          user + event written
                                          to DB (one transaction)
                                                        │
                                                   HTTP Response

(later, background worker)
wolverine_outgoing_envelopes → ProvisionKeycloakUserHandler
```

**Used for**: `ProvisionKeycloakUserEvent` — provisioning a Keycloak account after a user is created.

**On exception**: Wolverine catches the exception and applies the configured error handling policy (retry, dead-letter, …).

---

## 2. Transactional Outbox — How Atomicity Works

`PersistMessagesWithPostgresql` + `UseEntityFrameworkCoreTransactions` ensure that the DB write and the outbox event write happen in a **single transaction**.

```csharp
// CreateUserCommandHandler.HandleAsync()
await repository.AddAsync(user, ct);
await unitOfWork.CommitAsync(ct);
// ↑ one transaction writes:
//   1. AppUser         → identity.users
//   2. ProvisionKeycloakUserEvent → wolverine_outgoing_envelopes
```

**Guarantee**: if the DB commit succeeded, the event will be delivered — even after an app crash or restart.  
If the DB commit failed, the event is never written — no ghost messages.

> **Source**: [Wolverine docs — Durable Messaging](https://wolverinefx.net/guide/durability/)  
> *"What you need is to guarantee that both the outgoing messages and the database changes succeed or fail together."*

---

## 3. Durable Local Queues

```csharp
options.Policies.UseDurableLocalQueues();
```

All local queues (events dispatched between handlers) are persisted in PostgreSQL.  
If the app crashes between the DB commit and the handler execution, the event remains in the DB and Wolverine will process it after restart.

Without this setting, local queues are in-memory — messages are lost on crash.

---

## 4. Error Handling — Available Actions

| Action | Description | Works with `InvokeAsync`? |
|---|---|---|
| `Retry` | Immediately retry inline without any pause | ✅ yes |
| `RetryWithCooldown` | Wait X ms, then retry inline | ✅ yes |
| `ScheduleRetry` | Persist next attempt time to DB, release thread | ❌ no |
| `Requeue` | Put the message at the back of the queue | ❌ no |
| `Discard` | Drop the message, no further attempts | ❌ no |
| `MoveToErrorQueue` | Move to `wolverine_dead_letters` | ❌ no |
| `PauseProcessing` | Stop all workers for a given duration | ❌ no |

---

## 5. Configuration in `Program.cs`

```csharp
builder.Host.UseWolverine(options =>
{
    // Where to store outbox messages, dead-letter queue, and node state.
    // Creates tables: wolverine_outgoing_envelopes
    //                 wolverine_incoming_envelopes
    //                 wolverine_dead_letters
    options.PersistMessagesWithPostgresql(connectionString);

    // Integrates EF Core transactions with the Wolverine outbox.
    // The outbox write happens in THE SAME transaction as DbContext.SaveChangesAsync().
    // Only applies to DbContext types registered via AddDbContextWithWolverineIntegration.
    options.UseEntityFrameworkCoreTransactions();

    // All local queues persist in PostgreSQL.
    // Guarantees event delivery even after an app crash.
    options.Policies.UseDurableLocalQueues();

    // Retry policy for HttpRequestException (e.g. Keycloak temporarily unavailable).
    // APPLIES ONLY to queue-delivered messages (outbox worker) — NOT to InvokeAsync.
    // Attempt 1 fails → schedule retry in 5s
    // Attempt 2 fails → schedule retry in 30s
    // Attempt 3 fails → schedule retry in 5min
    // Attempt 4 fails → move to wolverine_dead_letters
    options.OnException<HttpRequestException>()
        .ScheduleRetry(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5))
        .Then.MoveToErrorQueue();
});
```

**Why `ScheduleRetry` and not `RetryWithCooldown`?**  
`RetryWithCooldown` waits inline, blocking the worker thread — suitable for short delays (milliseconds).  
`ScheduleRetry` persists the next attempt time to DB and releases the worker thread — suitable for longer delays (seconds, minutes).

---

## 6. `ProvisionKeycloakUserEvent` Lifecycle

```
CreateUserCommandHandler.HandleAsync()
    │
    ├─ user written to DB ──────────────────────────────────┐
    └─ event written to wolverine_outgoing_envelopes ───────┘  (one transaction)
    │
    HTTP Response ← controller returns immediately

(background worker — independent of the HTTP request)
    │
    ▼
ProvisionKeycloakUserHandler.HandleAsync()
    │
    ├── SUCCESS
    │     user.KeycloakUserId = "kc-xxx"
    │     saved to DB
    │     event deleted from wolverine_outgoing_envelopes
    │
    └── HttpRequestException (Keycloak down)
          ├── Retry after 5s  → failure
          ├── Retry after 30s → failure
          ├── Retry after 5min → failure
          └── MoveToErrorQueue
                → wolverine_dead_letters
                → user exists in DB without KeycloakUserId (cannot log in)
                → manual intervention required
```

---

## 7. Handler Idempotency

The handler is safe to run multiple times — the outcome is always the same.

```csharp
// ProvisionKeycloakUserHandler — skip if already provisioned
if (user is null || user.KeycloakUserId is not null)
    return OutgoingMessagesHelper.Empty;
```

```csharp
// KeycloakAdminService — if Keycloak returns 409 Conflict
// (user already exists from a previous retry attempt)
if (response.StatusCode == HttpStatusCode.Conflict)
{
    keycloakUserId = await GetExistingUserIdByUsernameAsync(username, ct);
    return keycloakUserId;
}
```

---

## 8. PostgreSQL Tables

| Table | Contents |
|---|---|
| `wolverine_outgoing_envelopes` | Events waiting to be delivered by the background worker |
| `wolverine_incoming_envelopes` | Incoming messages from external transports (RabbitMQ, etc.) |
| `wolverine_dead_letters` | Messages that exhausted all retries — manual intervention / replay required |

---

## 9. Multiple Application Instances

`PersistMessagesWithPostgresql` + `UseDurableLocalQueues` → all instances read from the same PostgreSQL table.  
Wolverine uses `SELECT ... FOR UPDATE SKIP LOCKED` — each message is processed **exactly once**, even across many instances.

---

## 10. Summary — What Goes Where

| What | Where | Applies to |
|---|---|---|
| Retry on exception | `Program.cs` → `options.OnException<T>()` | Outbox/queue handlers only |
| Outbox persistence | `PersistMessagesWithPostgresql` | All outgoing events |
| Atomic user + event write | `UseEntityFrameworkCoreTransactions` | EF Core DbContext handlers |
| Crash recovery | `UseDurableLocalQueues` | All local events |
| Idempotency | Inside the handler itself | Protection against duplicate delivery |
| Inline processing | `bus.InvokeAsync()` in controller | Direct command handlers |
