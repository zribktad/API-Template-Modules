using Microsoft.AspNetCore.Authorization;

namespace APITemplate.Api.Authorization;

/// <summary>
/// Authorization requirement that represents a named permission that a user must hold.
/// Evaluated by <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
