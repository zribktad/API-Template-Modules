using Microsoft.AspNetCore.Authorization;

namespace Identity.Auth.Security;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
