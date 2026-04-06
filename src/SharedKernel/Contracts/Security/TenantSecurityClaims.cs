namespace SharedKernel.Contracts.Security;

/// <summary>
///     Claim types used for multi-tenant HTTP concerns (output cache, etc.). Keep aligned with IdP mapping.
/// </summary>
public static class TenantSecurityClaims
{
    public const string TenantId = "tenant_id";
}
