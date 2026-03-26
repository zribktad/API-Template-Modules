using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Webhooks;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Webhooks;

public class HmacWebhookPayloadValidatorTests
{
    private const string TestSecret = "test-webhook-secret-at-least-16";

    private static HmacWebhookPayloadValidator CreateValidator(
        TimeProvider timeProvider,
        int toleranceSeconds = 300
    )
    {
        var options = Options.Create(
            new WebhookOptions { Secret = TestSecret, TimestampToleranceSeconds = toleranceSeconds }
        );
        return new HmacWebhookPayloadValidator(options, timeProvider);
    }

    [Fact]
    public void IsValid_CorrectHmac_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = CreateValidator(new FakeTimeProvider(now));
        var payload = """{"event":"test"}""";
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var signature = WebhookTestHelper.ComputeHmacSignature(payload, timestamp, TestSecret);

        validator.IsValid(payload, signature, timestamp).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WrongHmac_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = CreateValidator(new FakeTimeProvider(now));
        var payload = """{"event":"test"}""";
        var timestamp = now.ToUnixTimeSeconds().ToString();

        validator.IsValid(payload, "wrong-signature", timestamp).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_TimestampWithinTolerance_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = CreateValidator(new FakeTimeProvider(now), toleranceSeconds: 300);
        var payload = """{"event":"test"}""";
        var pastTimestamp = (now.ToUnixTimeSeconds() - 200).ToString();
        var signature = WebhookTestHelper.ComputeHmacSignature(payload, pastTimestamp, TestSecret);

        validator.IsValid(payload, signature, pastTimestamp).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_TimestampOutsideTolerance_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = CreateValidator(new FakeTimeProvider(now), toleranceSeconds: 300);
        var payload = """{"event":"test"}""";
        var oldTimestamp = (now.ToUnixTimeSeconds() - 600).ToString();
        var signature = WebhookTestHelper.ComputeHmacSignature(payload, oldTimestamp, TestSecret);

        validator.IsValid(payload, signature, oldTimestamp).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ExactBoundary_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = CreateValidator(new FakeTimeProvider(now), toleranceSeconds: 300);
        var payload = """{"event":"boundary"}""";
        var boundaryTimestamp = (now.ToUnixTimeSeconds() - 300).ToString();
        var signature = WebhookTestHelper.ComputeHmacSignature(
            payload,
            boundaryTimestamp,
            TestSecret
        );

        validator.IsValid(payload, signature, boundaryTimestamp).ShouldBeTrue();
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
