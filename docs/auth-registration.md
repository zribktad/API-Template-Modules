# Authentication registration map

This document is the **maintainer index** for where authentication and related middleware are registered. For protocol behavior, BFF storage, and tokens, see [AUTHENTICATION.md](AUTHENTICATION.md).

## DI registration order in `Program.cs`

The API host calls, in order:

1. **API Foundation Registrations** — individual components registered in `Program.cs` via extensions:
   - `services.AddRequestContext()`
   - `services.AddApiVersioningRegistration()`
   - `services.AddRequestValidation()`
   - `services.AddErrorHandling(configuration)`
   - `services.AddMvcConventions()`
   - `services.AddRedisInfrastructure(configuration)`
   - `services.AddCaching(configuration)`
   - `services.AddRateLimiting(configuration)`
   - `services.AddOpenApiDocumentation()`
   
   These provide output cache policies, DragonFly/in-memory cache, Data Protection, OpenAPI, etc. (Extracted from old `ApiServiceCollectionExtensions.cs` into individual files in `src/APITemplate/Api/Extensions/`)
2. **`AddModuleHealthChecks`**
3. **`AddIdentityModule`** — see below
4. **`AddObservability`**
5. Other feature modules (ProductCatalog, Reviews, …)

## Identity and Authentication Registration

- `AddIdentityModule` registers the default authentication scheme **JWT Bearer** and calls `AddAuthorization()`.
- It also registers **Cookie** and **OpenID Connect** schemes, `PostConfigure<JwtBearerOptions>` (wires `IdentityTokenValidatedPipeline` and challenge behavior), BFF session services, and authorization policies (fallback + roles).

The registration of the default scheme and authorization must happen before other modules can contribute their specific authorization requirements.

## HTTP middleware order

Configured in [`ApplicationBuilderExtensions.cs`](../src/APITemplate/Api/Extensions/Startup/ApplicationBuilderExtensions.cs) (`UseApiPipeline`): exception handler, HTTPS, correlation, **`UseAuthentication`**, **`CsrfValidationMiddleware`**, **`UseAuthorization`**, request context / Serilog, **`UseOutputCache`**, API docs.

CSRF runs after authentication so cookie-authenticated requests can be validated against the CSRF header contract.

## Component map

| Area | Primary location | Notes |
| --- | --- | --- |
| JWT Bearer defaults + `AddAuthorization()` shell | [`IdentityModule.Auth.cs`](../src/Modules/Identity/IdentityModule.Auth.cs) | Authority/audience; `OnTokenValidated` attached via `PostConfigure` |
| Cookie + OIDC BFF, session store, refresh coordinator, policies, `PostConfigure<JwtBearerOptions>` | [`IdentityModule.Auth.cs`](../src/Modules/Identity/IdentityModule.Auth.cs) | DragonFly vs in-memory BFF store follows `IsRedisConfigured()` |
| Post-login token validation (claims, tenant, user access) | [`IdentityTokenValidatedPipeline.cs`](../src/Modules/Identity/Auth/Security/IdentityTokenValidatedPipeline.cs) | JWT Bearer + OIDC `OnTokenValidated` |
| CSRF for cookie auth | [`CsrfValidationMiddleware.cs`](../src/APITemplate/Api/Middleware/CsrfValidationMiddleware.cs) | After `UseAuthentication` |
| BFF HTTP surface | [`BffController.cs`](../src/Modules/Identity/Auth/Features/V1/BffController.cs) | login, logout, user, csrf |
| Constants (schemes, routes, CSRF) | [`AuthConstants.cs`](../src/Modules/Identity/Auth/Common/Security/AuthConstants.cs) | |

## Related test pointers

See the **Authentication test matrix** in [testing.md](testing.md).
