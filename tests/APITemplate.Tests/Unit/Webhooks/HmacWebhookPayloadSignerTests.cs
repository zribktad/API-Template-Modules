using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Webhooks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Webhooks;

public class HmacWebhookPayloadSignerTests
{
    private const string TestSecret = "test-webhook-secret-at-least-16";

    private static HmacWebhookPayloadSigner CreateSigner(TimeProvider timeProvider) =>
        new(Options.Create(new WebhookOptions { Secret = TestSecret }), timeProvider);

    private static HmacWebhookPayloadValidator CreateValidator(TimeProvider timeProvider) =>
        new(Options.Create(new WebhookOptions { Secret = TestSecret }), timeProvider);

    [Fact]
    public void Sign_ProducesSignatureThatValidatorAccepts()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var signer = CreateSigner(timeProvider);
        var validator = CreateValidator(timeProvider);

        var payload = """{"jobId":"abc","status":"Completed"}""";

        var result = signer.Sign(payload);

        validator.IsValid(payload, result.Signature, result.Timestamp).ShouldBeTrue();
    }

    [Fact]
    public void Sign_DifferentPayloads_ProduceDifferentSignatures()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var signer = CreateSigner(timeProvider);

        var result1 = signer.Sign("payload-one");
        var result2 = signer.Sign("payload-two");

        result1.Signature.ShouldNotBe(result2.Signature);
    }

    [Fact]
    public void Sign_TimestampReflectsTimeProvider()
    {
        var fixedTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var signer = CreateSigner(timeProvider);

        var result = signer.Sign("test");

        result.Timestamp.ShouldBe(fixedTime.ToUnixTimeSeconds().ToString());
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
