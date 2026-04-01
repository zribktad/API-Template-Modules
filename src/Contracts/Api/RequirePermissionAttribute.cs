using Microsoft.AspNetCore.Authorization;

namespace Contracts.Api;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(policy: permission) { }
}
