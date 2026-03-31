using Microsoft.AspNetCore.Authorization;

namespace APITemplate.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
