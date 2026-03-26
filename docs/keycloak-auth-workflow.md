# Keycloak Authentication Workflow

This document describes how authentication and identity management work in this API.

## Architecture Overview

Authentication is fully delegated to Keycloak. The local database stores only business data — no passwords, no credential management.

- **Keycloak** = single source of truth for credentials and identity
- **Local DB** = business data only (AppUser stores profile + tenant assignment)

## Authentication Flows

### Browser (BFF + OIDC)
- Frontend redirects to `GET /api/v1/bff/login`
- Server-side OIDC flow redirects to Keycloak login page
- After login, tokens are stored server-side; a secure cookie session is issued to the browser
- Subsequent requests use the cookie; the server validates against Keycloak's JWKS

### Mobile (Direct OIDC + PKCE)
- Mobile app initiates OIDC Authorization Code flow with PKCE directly against Keycloak
- Keycloak client `api-template-mobile` (public client, no secret)
- Bearer JWT token is sent with each API request
- API validates JWT signature against Keycloak's JWKS endpoint

## User Registration

Registration is handled via the Keycloak Admin API — the application never sees or stores passwords.

1. `POST /api/v1/users` with `{ username, email }` (no password)
2. API calls Keycloak Admin API to create the user
3. Keycloak sends a setup email with `VERIFY_EMAIL` and `UPDATE_PASSWORD` actions
4. User clicks the email link, sets their password directly in Keycloak
5. AppUser record is created in local DB with `KeycloakUserId` linking to Keycloak

**Keycloak-first ordering:** If the DB save fails after Keycloak user creation, a compensating delete is called to remove the Keycloak user. If the compensating delete also fails, the orphaned Keycloak user must be cleaned up manually.

## Password Reset

Password reset is fully handled by Keycloak — no tokens stored in the local database.

1. `POST /api/v1/users/password-reset` with `{ email }` (anonymous endpoint)
2. API looks up the user by email
3. If found, calls Keycloak's `execute-actions-email` with `UPDATE_PASSWORD`
4. Keycloak sends the reset email directly to the user
5. User clicks the email link and sets a new password in Keycloak

The endpoint returns `200 OK` regardless of whether the user exists (prevents user enumeration).

## Invitation Flow

Tenants can invite users by email. The flow integrates Keycloak registration with local tenant provisioning.

1. Tenant admin creates an invitation: `POST /api/v1/invitations` with `{ email }`
2. System sends an invitation email with a unique token
3. User clicks the link and is redirected to Keycloak registration
4. After Keycloak registration, user logs in for the first time
5. On first login, `UserProvisioningService` runs inside `OnTokenValidated`:
   - Looks for an accepted `TenantInvitation` by the user's normalized email
   - Creates an `AppUser` record with `TenantId` from the invitation and `KeycloakUserId` from the JWT `sub` claim
6. The user is now provisioned and can access their tenant's resources

**Important:** The Keycloak token must contain a `tenant_id` attribute claim for the user to pass authorization. This requires a Keycloak mapper to be configured (see Keycloak Configuration below).

## Activate / Deactivate Users

Activating or deactivating a user syncs the state to both the local DB and Keycloak:
- `PATCH /api/v1/users/{id}/activate` → sets `IsActive = true` in DB + `enabled = true` in Keycloak
- `PATCH /api/v1/users/{id}/deactivate` → sets `IsActive = false` in DB + `enabled = false` in Keycloak

Users without a `KeycloakUserId` (legacy users created before this migration) have DB-only state changes.

## Keycloak Configuration

### Required: Service Account Permissions

The API client (`api-template`) must have a service account with the following role:
- `realm-management` → `manage-users`

This allows the API to call Keycloak Admin API endpoints.

### Required: tenant_id Claim Mapper

For users to pass tenant authorization, Keycloak must include a `tenant_id` attribute in the JWT. Configure a mapper on the `api-template` client:
- Mapper type: **User Attribute**
- User attribute name: `tenant_id`
- Token claim name: `tenant_id`
- Claim JSON type: `String`
- Add to ID token, Access token, Userinfo: Yes

Tenant admins must set the `tenant_id` user attribute in Keycloak when provisioning users.

### Optional: Mobile Client

For mobile apps using PKCE:
- Create a new client: `api-template-mobile`
- Client type: **Public** (no secret)
- Valid redirect URIs: your app's deep link scheme
- Standard flow: enabled
- PKCE method: S256

## AppUser Entity

| Field | Description |
|---|---|
| `Id` | Local UUID primary key |
| `KeycloakUserId` | Keycloak subject ID (`sub` claim) — nullable for legacy users |
| `Username` | Display name |
| `Email` | User email |
| `TenantId` | Tenant this user belongs to |
| `IsActive` | Whether the user can access the system |
| `Role` | Application role (User, TenantAdmin, PlatformAdmin) |

No password hash is stored locally.
