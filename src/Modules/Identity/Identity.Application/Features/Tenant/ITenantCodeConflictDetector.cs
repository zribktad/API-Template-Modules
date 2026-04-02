namespace Identity.Application.Features.Tenant;

public interface ITenantCodeConflictDetector
{
    bool IsCodeConflict(Exception exception);
}
