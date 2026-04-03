using Ardalis.Specification;
using Identity.Application.Features.Tenant.DTOs;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Search;
using TenantEntity = Identity.Domain.Entities.Tenant;

namespace Identity.Application.Features.Tenant.Specifications;

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
