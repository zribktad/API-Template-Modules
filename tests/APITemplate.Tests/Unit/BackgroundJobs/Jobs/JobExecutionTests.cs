using BackgroundJobs.Domain;
using ErrorOr;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

[Trait("Category", "Unit")]
public sealed class JobExecutionTests
{
    private static JobExecution CreatePendingJob()
    {
        return JobExecution.Create("test-job", TimeProvider.System);
    }

    private static TimeProvider MockTimeAt(DateTime utcNow)
    {
        Mock<TimeProvider> mock = new();
        mock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(utcNow));
        return mock.Object;
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkProcessing_FromPending_Succeeds()
    {
        JobExecution sut = CreatePendingJob();
        DateTime now = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        ErrorOr<Success> result = sut.MarkProcessing(MockTimeAt(now));

        result.IsError.ShouldBeFalse();
        sut.Status.ShouldBe(JobStatus.Processing);
        sut.StartedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkCompleted_FromProcessing_Succeeds()
    {
        JobExecution sut = CreatePendingJob();
        DateTime now = new(2026, 1, 1, 10, 5, 0, DateTimeKind.Utc);
        TimeProvider time = MockTimeAt(now);
        sut.MarkProcessing(time);

        ErrorOr<Success> result = sut.MarkCompleted("result-payload", time);

        result.IsError.ShouldBeFalse();
        sut.Status.ShouldBe(JobStatus.Completed);
        sut.ProgressPercent.ShouldBe(100);
        sut.ResultPayload.ShouldBe("result-payload");
        sut.CompletedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkCompleted_WithNullPayload_Succeeds()
    {
        JobExecution sut = CreatePendingJob();
        TimeProvider time = MockTimeAt(DateTime.UtcNow);
        sut.MarkProcessing(time);

        ErrorOr<Success> result = sut.MarkCompleted(null, time);

        result.IsError.ShouldBeFalse();
        sut.ResultPayload.ShouldBeNull();
        sut.Status.ShouldBe(JobStatus.Completed);
    }

    [Fact]
    public void MarkFailed_FromProcessing_Succeeds()
    {
        JobExecution sut = CreatePendingJob();
        DateTime now = new(2026, 1, 1, 10, 10, 0, DateTimeKind.Utc);
        TimeProvider time = MockTimeAt(now);
        sut.MarkProcessing(time);

        ErrorOr<Success> result = sut.MarkFailed("something went wrong", time);

        result.IsError.ShouldBeFalse();
        sut.Status.ShouldBe(JobStatus.Failed);
        sut.ErrorMessage.ShouldBe("something went wrong");
        sut.CompletedAtUtc.ShouldBe(now);
    }

    // ── State-transition guards ───────────────────────────────────────────────

    [Fact]
    public void MarkProcessing_WhenAlreadyProcessing_ReturnsConflictError()
    {
        JobExecution job = CreatePendingJob();
        TimeProvider time = TimeProvider.System;
        job.MarkProcessing(time);

        ErrorOr<Success> result = job.MarkProcessing(time);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Job.InvalidTransition");
        result.FirstError.Description.ShouldContain("Processing");
    }

    [Fact]
    public void MarkProcessing_WhenCompleted_ReturnsConflictError()
    {
        JobExecution job = CreatePendingJob();
        TimeProvider time = TimeProvider.System;
        job.MarkProcessing(time);
        job.MarkCompleted(null, time);

        ErrorOr<Success> result = job.MarkProcessing(time);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void MarkCompleted_FromPending_ReturnsConflictError()
    {
        JobExecution job = CreatePendingJob();

        ErrorOr<Success> result = job.MarkCompleted(null, TimeProvider.System);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Job.InvalidTransition");
        result.FirstError.Description.ShouldContain("Completed");
    }

    [Fact]
    public void MarkCompleted_WhenAlreadyCompleted_ReturnsConflictError()
    {
        JobExecution job = CreatePendingJob();
        TimeProvider time = TimeProvider.System;
        job.MarkProcessing(time);
        job.MarkCompleted(null, time);

        ErrorOr<Success> result = job.MarkCompleted(null, time);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void MarkFailed_FromPending_ReturnsConflictError()
    {
        JobExecution job = CreatePendingJob();

        ErrorOr<Success> result = job.MarkFailed("err", TimeProvider.System);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Job.InvalidTransition");
        result.FirstError.Description.ShouldContain("Failed");
    }

    [Fact]
    public void MarkFailed_WhenAlreadyCompleted_ReturnsConflictError()
    {
        JobExecution job = CreatePendingJob();
        TimeProvider time = TimeProvider.System;
        job.MarkProcessing(time);
        job.MarkCompleted(null, time);

        ErrorOr<Success> result = job.MarkFailed("late error", time);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    // ── Other domain behaviour ────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 50)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(-1, 0)]
    [InlineData(101, 100)]
    public void UpdateProgress_ClampsToValidRange(int input, int expected)
    {
        JobExecution sut = CreatePendingJob();

        sut.UpdateProgress(input);

        sut.ProgressPercent.ShouldBe(expected);
    }

    [Fact]
    public void NewJob_HasPendingStatusAndZeroProgress()
    {
        JobExecution sut = CreatePendingJob();

        sut.Status.ShouldBe(JobStatus.Pending);
        sut.ProgressPercent.ShouldBe(0);
        sut.StartedAtUtc.ShouldBeNull();
        sut.CompletedAtUtc.ShouldBeNull();
    }
}
