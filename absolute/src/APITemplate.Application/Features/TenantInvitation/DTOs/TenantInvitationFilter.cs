using APITemplate.Application.Common.DTOs;
using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.TenantInvitation.DTOs;

/// <summary>
/// Pagination and filtering parameters for querying tenant invitations, supporting optional email and status filters.
/// </summary>
public sealed record TenantInvitationFilter(
    string? Email = null,
    InvitationStatus? Status = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize);
