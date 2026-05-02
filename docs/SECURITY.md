# Security Overview

This document provides a comprehensive overview of the security architecture and measures implemented in the APITemplate project to protect data, users, and infrastructure.

## 1. Identity & Access Management (IAM)

The project centralizes identity management using **Keycloak** as a dedicated Identity Provider (IdP).

### Authentication
- **JWT Bearer:** Used for direct API access (mobile apps, microservices). Validated via the OIDC discovery endpoint.
- **BFF (Backend-for-Frontend):** Used for browser-based SPAs. It uses OIDC with HttpOnly cookies, ensuring that sensitive access/refresh tokens never reach the JavaScript environment.
- **OAuth2 with PKCE:** Enforced for all interactive flows (Scalar UI, mobile apps) to prevent authorization code injection attacks.

### Authorization
- **Role-Based Access Control (RBAC):** Claims from Keycloak (e.g., `realm_access.roles`) are mapped to .NET `ClaimTypes.Role`.
- **Permission-Based Security:** Granular access is enforced using permission attributes (e.g., `[RequirePermission]`) and specialized policies like `PlatformAdmin`.
- **Fallback Policy:** By default, all endpoints require authentication unless explicitly marked with `[AllowAnonymous]`.

> **Detailed Doc:** See [AUTHENTICATION.md](AUTHENTICATION.md).

---

## 2. Data Isolation & Multi-tenancy

The application is built with a multi-tenant architecture, ensuring strict data isolation between different organizations.

### Global Query Filters
- **Tenant Isolation:** Every entity implementing `ITenantEntity` is automatically filtered by the current `TenantId` via EF Core global query filters. This prevents data leakage even if a developer forgets to add a `Where` clause.
- **Soft Delete:** Entities implementing `ISoftDeletable` are automatically filtered to exclude deleted records (`IsDeleted == false`).

### Implementation
- Managed in `ModuleDbContext.cs` via `HasQueryFilter`.
- Bypassed only in specific system contexts (e.g., background cleanup jobs) using `.IgnoreQueryFilters()`.

---

## 3. Cryptographic Protection

### Data Protection (Encryption at Rest)
- **Session Tokens:** Access, Refresh, and ID tokens stored in the `BffPersistedSession` table are encrypted using the ASP.NET Core **Data Protection** API (`IDataProtector`).
- **CSRF Tokens:** Anti-forgery tokens are cryptographically bound to the user's session and encrypted.

### Shared Key Ring (Redis)
- In multi-instance deployments, the Data Protection key ring is persisted to **Redis/DragonFly**. This ensures that a session cookie or CSRF token issued by instance A can be correctly decrypted and validated by instance B.

---

## 4. Network & Transport Security

### HTTPS & HSTS
- **Strict HTTPS:** All traffic is redirected to HTTPS via `app.UseHttpsRedirection()`.
- **HSTS:** Enabled in production to prevent protocol downgrade attacks (SSL Stripping).
  HSTS (HTTP Strict Transport Security) is a response header that tells the browser: "Always use HTTPS to connect to this domain. Never try HTTP again." It protects against Man-in-the-Middle (MitM) attacks where an attacker might try to intercept the initial unsecured HTTP request.

  The behavior is controlled in `appsettings.json` under the `"Hsts"` section:
  - **`MaxAgeDays`:** How long (in days) the browser remembers this rule. Default is 365 days. This protects the exact domain the API is running on.
  - **`IncludeSubDomains`:** If true, tells the browser that *all* subdomains (e.g., `dev.api...`) must also use HTTPS. Use with caution if you have legacy HTTP subdomains.
  - **`Preload`:** The strictest setting. If true, adds the `preload` directive. If you register your domain at *hstspreload.org*, major browsers (Chrome, Safari, Firefox) will hardcode your domain as HTTPS-only. This protects users even on their very first visit, but it is very difficult to undo.
- **TLS:** Only secure TLS versions are supported by the hosting environment.

### Security Response Headers
Configured globally in `SecurityHeadersExtensions.cs`:
- **CSP (Content Security Policy):** Strict "API-first" policy (`default-src 'none'`) to mitigate XSS and data injection.
- **X-Frame-Options (DENY):** Prevents Clickjacking.
- **X-Content-Type-Options (nosniff):** Prevents MIME-sniffing attacks.
- **Referrer-Policy:** Protects sensitive data in URLs.
- **Permissions-Policy:** Denies access to sensitive browser APIs (camera, microphone).

---

## 5. Application-Level Defenses

### CSRF Protection
- **Enforcement:** `CsrfValidationMiddleware` requires a valid `X-CSRF` header for all mutating requests (POST/PUT/DELETE) originating from browser-based clients (BFF Cookie flow).
- **Exemptions:** Requests using JWT Bearer tokens are exempt as they are inherently protected against CSRF.

### Rate Limiting
- **Throttling:** Implemented to prevent DoS and brute-force attacks.
- **Policies:** Includes both global limits and per-user partitioning.
- **Detailed Doc:** See [RATE_LIMITING.md](RATE_LIMITING.md).

### Request Validation
- All inputs are strictly validated using **FluentValidation** and Data Annotations before reaching the business logic.

---

## 6. Observability & Auditing

### Audit Stamps
- Entities implementing `IAuditableTenantEntity` automatically track:
    - `CreatedAtUtc` / `CreatedBy` (Actor ID)
    - `LastModifiedAtUtc` / `LastModifiedBy`
- Managed automatically by `AuditableEntityStateManager` during `SaveChanges`.

### Sensitive Data Redaction
- **Log Masking:** Sensitive information (PII) is automatically redacted from logs and telemetry using `Microsoft.Extensions.Compliance.Redaction`.
- **PII Tags:** Data is tagged (e.g., `[DataAttributes.Pii]`) to ensure correct handling by the redaction engine.

---

## 7. Secure Development Lifecycle

### Vulnerability Scanning
- **NuGet Audit:** The project uses centralized package management (`Directory.Packages.props`) and enables NuGet Audit to catch vulnerable dependencies during build.
- **Static Analysis:** Tools like **Qodana** and GitHub **CodeQL** are used to scan the codebase for security flaws and code quality issues.

### Secret Management
- **Environment Variables:** Secrets (API keys, DB strings) are never stored in the repository. They are injected via environment variables or Docker secrets.
- **Development Secrets:** `appsettings.Development.json` is used for local dev only; production secrets are managed in secure vaults (e.g., Azure Key Vault, HashiCorp Vault) during deployment.
