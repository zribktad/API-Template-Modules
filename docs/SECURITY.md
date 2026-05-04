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

### Shared Key Ring (PostgreSQL)
- In multi-instance deployments, the Data Protection key ring is persisted to **PostgreSQL** via EF Core (`IdentityDbContext`). This ensures that a session cookie or CSRF token issued by instance A can be correctly decrypted and validated by instance B, while protecting keys from being lost during cache evictions or restarts of the Redis/DragonFly service.

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

### Request Size Limits
To mitigate Large Payload Denial-of-Service (DoS) attacks, the API enforces strict request body size limits:
- **Default Limit:** 1 MB (configured via `Request:RequestSizeLimitMb`). Standard ASP.NET Core defaults to 30 MB, which is excessively high for most REST/GraphQL API calls and can be exploited to exhaust server memory/bandwidth.
- **Overrides:** Specific endpoints that require larger payloads (e.g., file uploads in `FileStorage`) use the `[RequestSizeLimit]` attribute to safely allow larger requests (e.g., 10 MB or 100 MB) without compromising the rest of the system.

### Modern Browser Isolation Headers
The API includes high-security headers to protect against side-channel attacks (like Spectre) and cross-origin information leaks:
- **COOP (Cross-Origin-Opener-Policy):** Set to `same-origin`. Ensures the document does not share a browsing context group with cross-origin documents.
- **COEP (Cross-Origin-Embedder-Policy):** Set to `require-corp`. Prevents the document from loading any cross-origin resources that don't explicitly grant permission via CORP.
- **CORP (Cross-Origin-Resource-Policy):** Set to `same-origin`. Prevents other origins from loading resources from this API.

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
- **Distributed State:** Uses **Redis/Dragonfly** as the backplane via `RedisRateLimiting`. This ensures that limits are synchronized across all API instances, preventing users from bypassing quotas by hitting different nodes in a cluster.
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
- **On-Demand Security Scan:** A dedicated workflow (`security-scan.yml`) provides deep container and dependency analysis (see Section 8).

### Secret Management
- **Environment Variables:** Secrets (API keys, DB strings) are never stored in the repository. They are injected via environment variables or Docker secrets.
- **Development Secrets:** `appsettings.Development.json` is used for local dev only; production secrets are managed in secure vaults (e.g., Azure Key Vault, HashiCorp Vault) during deployment.

---

## 8. Software Supply Chain Security (EU CRA 2027)

To align with the upcoming **EU Cyber Resilience Act (CRA)** requirements (enforced from 2027), the project implements proactive software supply chain security measures. These measures ensure transparency and minimize the risk of inheriting vulnerabilities from third-party components.

### Software Bill of Materials (SBOM)
- **Standard:** We use the **CycloneDX** standard to generate a machine-readable inventory of all software components, dependencies, and metadata.
- **Automation:** The `On-Demand Security Scan` workflow automatically generates a `bom.json` file for the entire solution.
- **Compliance:** This allows for rapid impact analysis when a new high-profile vulnerability (like Log4j) is discovered in a common library.

### Container Security (Trivy)
- **Scanning:** Every time the security scan is triggered, the project's Docker images are built and scanned using **Aqua Trivy**.
- **Scope:** The scan checks both the base OS layers and the application dependencies for known **CVEs (Common Vulnerabilities and Exposures)**.
- **Gatekeeping:** The scanner is configured with a strict "Fail-Fast" policy: if any **CRITICAL** severity vulnerability is detected, the workflow fails, preventing the deployment of insecure artifacts.

### Rationale
These tools move security "to the left" of the development cycle. By documenting every piece of code we ship (SBOM) and rigorously testing our final deployment units (Docker) against global vulnerability databases, we ensure that the application is not only secure by design but also secure by assembly.
