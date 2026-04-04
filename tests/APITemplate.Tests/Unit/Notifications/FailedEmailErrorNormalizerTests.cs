using Notifications.Domain;
using Notifications.Services;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

public sealed class FailedEmailErrorNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_WhenNullOrEmpty_ReturnsSame(string? error)
    {
        FailedEmailErrorNormalizer.Normalize(error).ShouldBe(error);
    }

    [Fact]
    public void Normalize_WhenShorterThanMax_ReturnsUnchanged()
    {
        string msg = new string('e', 100);

        FailedEmailErrorNormalizer.Normalize(msg).ShouldBe(msg);
    }

    [Fact]
    public void Normalize_WhenLongerThanMax_TruncatesToMaxLength()
    {
        string msg = new string('x', FailedEmail.LastErrorMaxLength + 50);

        string? normalized = FailedEmailErrorNormalizer.Normalize(msg);

        normalized!.Length.ShouldBe(FailedEmail.LastErrorMaxLength);
    }
}
