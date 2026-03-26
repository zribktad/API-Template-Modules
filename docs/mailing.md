# Mailing

The email system is built on Clean Architecture principles — the application layer defines abstractions, the infrastructure layer provides implementations.

## Architecture

```
Domain Event (INotification)
    ↓
EmailNotificationHandler   ← MediatR
    ↓
IEmailTemplateRenderer     ← renders Liquid template → HTML
    ↓
IEmailQueue                ← enqueues message into in-memory channel
    ↓
EmailSendingBackgroundService  ← reads from channel, sends via SMTP + Polly retry
    ↓
IEmailSender (MailKit)
```

## Components

### Abstractions (`Application.Common.Email`)

| Interface | Description |
|---|---|
| `IEmailQueue` | Enqueue messages for sending |
| `IEmailSender` | Send a single message via SMTP |
| `IEmailTemplateRenderer` | Render a Liquid template to HTML |
| `ISecureTokenGenerator` | Generate and hash tokens (password reset, invitation) |

### Implementations (`Infrastructure.Email`)

| Class | Description |
|---|---|
| `ChannelEmailQueue` | Bounded in-memory channel (capacity 1000, `SingleReader = true`) |
| `MailKitEmailSender` | SMTP sender using MailKit |
| `FluidEmailTemplateRenderer` | Liquid engine via Fluid.Core, caches parsed templates in-memory |
| `EmailSendingBackgroundService` | `BackgroundService` reading from the channel, sends with Polly retry pipeline |

### Domain Events (`Application.Common.Events`)

| Notification | When published |
|---|---|
| `UserRegisteredNotification` | After a user is created |
| `UserRoleChangedNotification` | After a user's role is changed |
| `TenantInvitationCreatedNotification` | After an invitation is created or resent |
| `PasswordResetRequestedNotification` | After a password reset is requested |

`EmailNotificationHandler` implements all four `INotificationHandler<T>` interfaces and translates events into `EmailMessage` → enqueue.

> Notification publishing in handlers (`UserRequestHandlers`, `TenantInvitationRequestHandlers`) is **best-effort** — wrapped in `try/catch` so a notification failure does not roll back an already-persisted change.

## Email Templates

Templates are Liquid files (`*.liquid`) stored as **embedded resources** in `APITemplate.Infrastructure`.

```
Infrastructure/Email/Templates/
├── user-registration.liquid
├── tenant-invitation.liquid
├── password-reset.liquid
└── user-role-changed.liquid
```

### Available variables

| Template | Variables |
|---|---|
| `user-registration` | `Username`, `Email`, `LoginUrl` |
| `tenant-invitation` | `Email`, `TenantName`, `InvitationUrl`, `ExpiryHours` |
| `password-reset` | `Username`, `ResetUrl`, `ExpiryMinutes` |
| `user-role-changed` | `Username`, `OldRole`, `NewRole` |

### Adding a new template

1. Create `Infrastructure/Email/Templates/<name>.liquid`
2. Add a constant to `EmailTemplateNames`
3. Add a new event and handler method in `EmailNotificationHandler`

## Resilience

A Polly pipeline is configured for SMTP sending under `ResiliencePipelineKeys.SmtpSend`:

- **Strategy:** exponential backoff with jitter
- **Max attempts:** `EmailOptions.MaxRetryAttempts` (default: 3)
- **Base delay:** `EmailOptions.RetryBaseDelaySeconds` (default: 2 s)

After all retry attempts are exhausted the message is **dropped** and the error is logged (`LogError`).

## Configuration

```json
"Email": {
  "SmtpHost": "localhost",
  "SmtpPort": 587,
  "UseSsl": true,
  "SenderEmail": "noreply@apitemplate.local",
  "SenderName": "APITemplate",
  "Username": null,
  "Password": null,
  "PasswordResetTokenExpiryMinutes": 60,
  "InvitationTokenExpiryHours": 72,
  "BaseUrl": "http://localhost:5000",
  "MaxRetryAttempts": 3,
  "RetryBaseDelaySeconds": 2
}
```

### Development (Mailpit / MailHog)

```json
"Email": {
  "SmtpHost": "localhost",
  "SmtpPort": 1025,
  "UseSsl": false
}
```

For local development we recommend [Mailpit](https://github.com/axllent/mailpit) — it captures outgoing emails without actually sending them.

## Service registration

```csharp
builder.Services.AddEmailServices(builder.Configuration);
```

Called in `Program.cs`, registers all dependencies including the `BackgroundService` and Polly pipeline.
