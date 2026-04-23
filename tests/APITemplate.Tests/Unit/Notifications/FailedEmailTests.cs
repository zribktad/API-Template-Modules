using APITemplate.Tests.Unit.Helpers;
using ErrorOr;
using Notifications.Domain;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

public class FailedEmailTests
{
    private readonly MutableFakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private FailedEmail CreateEmail() => FailedEmail.Create(
        "to@example.com", "Subject", "<p>body</p>", _time
    );

    [Fact]
    public void Create_SetsRequiredFields()
    {
        FailedEmail email = CreateEmail();

        email.To.ShouldBe("to@example.com");
        email.Subject.ShouldBe("Subject");
        email.HtmlBody.ShouldBe("<p>body</p>");
        email.RetryCount.ShouldBe(0);
        email.IsDeadLettered.ShouldBeFalse();
        email.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Claim_WhenNotClaimed_Succeeds()
    {
        FailedEmail email = CreateEmail();

        ErrorOr<Success> result = email.Claim("worker-1", _time, TimeSpan.FromMinutes(5));

        result.IsError.ShouldBeFalse();
        email.ClaimedBy.ShouldBe("worker-1");
        email.ClaimedUntilUtc.ShouldNotBeNull();
    }

    [Fact]
    public void Claim_WhenAlreadyClaimed_ReturnsError()
    {
        FailedEmail email = CreateEmail();
        email.Claim("worker-1", _time, TimeSpan.FromMinutes(5));

        ErrorOr<Success> result = email.Claim("worker-2", _time, TimeSpan.FromMinutes(5));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void Claim_AfterLeaseExpired_Succeeds()
    {
        FailedEmail email = CreateEmail();
        email.Claim("worker-1", _time, TimeSpan.FromMinutes(5));
        _time.Advance(TimeSpan.FromMinutes(6));

        ErrorOr<Success> result = email.Claim("worker-2", _time, TimeSpan.FromMinutes(5));

        result.IsError.ShouldBeFalse();
        email.ClaimedBy.ShouldBe("worker-2");
    }

    [Fact]
    public void RecordFailure_IncrementsRetryCount_AndClearsClaim()
    {
        FailedEmail email = CreateEmail();
        email.Claim("worker-1", _time, TimeSpan.FromMinutes(5));

        email.RecordFailure("SMTP timeout", _time);

        email.RetryCount.ShouldBe(1);
        email.LastError.ShouldBe("SMTP timeout");
        email.LastAttemptAtUtc.ShouldNotBeNull();
        email.ClaimedBy.ShouldBeNull();
        email.ClaimedAtUtc.ShouldBeNull();
        email.ClaimedUntilUtc.ShouldBeNull();
    }

    [Fact]
    public void RecordFailure_TruncatesLongError()
    {
        FailedEmail email = CreateEmail();
        string longError = new('x', FailedEmail.LastErrorMaxLength + 100);

        email.RecordFailure(longError, _time);

        email.LastError!.Length.ShouldBe(FailedEmail.LastErrorMaxLength);
    }

    [Fact]
    public void MarkDeadLettered_SetsFlag_AndClearsClaim()
    {
        FailedEmail email = CreateEmail();
        email.Claim("worker-1", _time, TimeSpan.FromMinutes(5));

        email.MarkDeadLettered();

        email.IsDeadLettered.ShouldBeTrue();
        email.ClaimedBy.ShouldBeNull();
        email.ClaimedUntilUtc.ShouldBeNull();
    }
}
