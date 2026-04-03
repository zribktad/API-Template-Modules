namespace Identity.Features.Tenant;

public interface ITenantCodeConflictDetector
{
    bool IsCodeConflict(Exception exception);
}

