using ErrorOr;
using Identity.Features.TenantInvitation.Specifications;

namespace Identity.Features.TenantInvitation;

public sealed record GetTenantInvitationsQuery(TenantInvitationFilter Filter);

public sealed class GetTenantInvitationsQueryHandler
{
    public static async Task<ErrorOr<PagedResponse<TenantInvitationResponse>>> HandleAsync(
        GetTenantInvitationsQuery request,
        ITenantInvitationRepository invitationRepository,
        CancellationToken ct
    )
    {
        return await invitationRepository.GetPagedAsync(
            new TenantInvitationFilterSpecification(request.Filter),
            request.Filter.PageNumber,
            request.Filter.PageSize,
            ct
        );
    }
}
