using Microsoft.AspNetCore.Authorization;

namespace APITemplate.Api.Authorization;

/// <summary>
/// Marks a controller or action as requiring a specific named permission.
/// The permission name is used as an ASP.NET Core authorization policy name,
/// which is evaluated by the policy-based authorization infrastructure.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes the attribute with the given permission name, applied as the authorization policy.
    /// </summary>
    public RequirePermissionAttribute(string permission)
        : base(policy: permission) { }
}
