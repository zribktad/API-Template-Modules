using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;

namespace APITemplate.Tests.Unit.Infrastructure;

internal static class OutgoingMessagesAssertions
{
    public static void ShouldContainCacheTags(
        this OutgoingMessages messages,
        IEnumerable<string> expectedTags,
        bool ignoreOrder = true
    )
    {
        messages
            .OfType<CacheInvalidationNotification>()
            .Select(x => x.CacheTag)
            .ToList()
            .ShouldBe(expectedTags.ToList(), ignoreOrder: ignoreOrder);
    }

    public static void ShouldContainSingleCacheTag(
        this OutgoingMessages messages,
        string expectedTag
    )
    {
        messages
            .OfType<CacheInvalidationNotification>()
            .Select(x => x.CacheTag)
            .ShouldHaveSingleItem()
            .ShouldBe(expectedTag);
    }
}
