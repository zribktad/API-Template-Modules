using Reviews;
using Reviews.Common.Events;
using SharedKernel.Contracts.Events;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Reviews.Events;

[Trait("Category", "Unit")]
public sealed class CacheInvalidationCascadesTests
{
    [Fact]
    public void ForReviewChange_ShouldContainReviewsAndCategories()
    {
        CacheInvalidationCascades
            .ForReviewChange.Select(x => x.CacheTag)
            .ShouldBe([CacheTags.Reviews, CacheTags.Categories], ignoreOrder: true);
    }
}
