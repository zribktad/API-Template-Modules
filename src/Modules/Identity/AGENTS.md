# Identity Module — AGENTS.md

## OVERVIEW
Authentication, BFF sessions, user management, roles, tenant invitations, Keycloak sync. LARGEST module (83 files). Uses nested sub-modules (Auth/, Directory/) — the ONLY module with this pattern.

## STRUCTURE
```
Identity/
├── Auth/                   ← SUB-MODULE: BFF sessions, OIDC, JWT, Cookie auth
│   ├── Common/
│   ├── Entities/           # BffPersistedSession
│   ├── Features/
│   ├── Handlers/
│   ├── Http/
│   ├── Security/           # RedisTicketStore, session management
│   └── Validation/
├── Directory/              ← SUB-MODULE: Users, roles, tenants, Keycloak sync
│   ├── Common/
│   ├── Domain/Services/
│   ├── Entities/           # AppUser, CustomRole, RolePermission, Tenant, TenantInvitation
│   ├── Enums/
│   ├── Features/
│   ├── Handlers/
│   ├── Interfaces/
│   ├── Repositories/
│   └── Security/
├── Common/                 # Cross-cutting Identity utilities
├── Configuration/          # IOptions setup
├── ValueObjects/
├── Persistence/            # IdentityDbContext, EF configs, migrations
├── Logging/
├── Migrations/             # EF Core migrations
├── IdentityModule.cs
├── IdentityModule.Auth.cs      # Partial: registers Auth sub-module
└── IdentityModule.Directory.cs # Partial: registers Directory sub-module
```

## WHERE TO LOOK
| Need | Location |
|------|----------|
| BFF login/logout/session | `Auth/Security/` |
| JWT validation | `Auth/Http/` |
| User CRUD | `Directory/Features/User/` |
| Role management | `Directory/Features/Role/` |
| Tenant operations | `Directory/Features/Tenant/` |
| Keycloak sync | `Directory/Handlers/` |
| Session cookie config | `Configuration/BffOptions.cs` |
| Auth-related events | `Directory/Features/User/Shared/` or `Auth/Features/` |

## CONVENTIONS
- Module registration split across partial classes: `IdentityModule.cs` + `.Auth.cs` + `.Directory.cs`.
- Auth/ and Directory/ each have their own internal Clean Architecture (Entities/, Features/, Handlers/, etc.).
- `Common/` folders contain shared errors and events within each sub-module.
- Keycloak admin SDK used for user provisioning sync.

## ANTI-PATTERNS
- NEVER add a third sub-module — Auth and Directory are the bounded contexts; don't expand further.
- NEVER reference Directory entities from Auth or vice versa — they're separate bounded contexts.
- NEVER bypass `IdentityDbContext` for auth data — all persistence goes through it.

## NOTES
- BFF sessions use DragonFly (Redis) as server-side store with L1 in-process cache + pub/sub revocation.
- Bootstrap seeding creates default tenant + admin user on first startup if none exist.
- `IdentityDbMarker.cs` — empty marker type for EF Core design-time factory.
