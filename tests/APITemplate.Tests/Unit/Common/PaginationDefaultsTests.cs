using APITemplate.Api.GraphQL.Models;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Common;

public sealed class PaginationDefaultsTests
{
    [Fact]
    public void PagingDefaults_AreConsistentAcrossRestAndGraphQlFilters()
    {
        PaginationFilter.DefaultPageSize.ShouldBe(20);
        new PaginationFilter().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
        new CategoryFilter().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
        new ProductFilter().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
        new ProductReviewFilter().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
        new CategoryQueryInput().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
        new ProductQueryInput().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
        new ProductReviewQueryInput().PageSize.ShouldBe(PaginationFilter.DefaultPageSize);
    }
}
