namespace Identity.Features.Tenant;

public interface ITenantCodeConflictDetector
{
    public bool IsCodeConflict(Exception exception);
}
