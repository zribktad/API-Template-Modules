using APITemplate.Application.Common.Search;
using APITemplate.Application.Features.Tenant.DTOs;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Specifications;

/// <summary>
/// Internal extension that applies shared <see cref="TenantFilter"/> criteria to an Ardalis specification builder.
/// </summary>
internal static class TenantFilterCriteria
{
    /// <summary>
    /// Adds a PostgreSQL full-text search predicate on <c>Code</c> and <c>Name</c> when <see cref="TenantFilter.Query"/> is provided.
    /// </summary>
    internal static void ApplyFilter(
        this ISpecificationBuilder<TenantEntity> query,
        TenantFilter filter
    )
    {
        if (string.IsNullOrWhiteSpace(filter.Query))
            return;

        query.Where(tenant =>
            EF.Functions.ToTsVector(
                    SearchDefaults.TextSearchConfiguration,
                    tenant.Code + " " + tenant.Name
                )
                .Matches(
                    EF.Functions.WebSearchToTsQuery(
                        SearchDefaults.TextSearchConfiguration,
                        filter.Query
                    )
                )
        );
    }
}
