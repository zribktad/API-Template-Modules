using BackgroundJobs.Shared;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

public sealed class JobExecutionTests
{
    private static JobExecution CreatePendingJob() =>
        new()
        {
            Id = Guid.NewGuid(),
            JobType = "test-job",
            SubmittedAtUtc = DateTime.UtcNow,
        };

    private static TimeProvider MockTimeAt(DateTime utcNow)
    {
        Mock<TimeProvider> mock = new();
        mock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(utcNow));
        return mock.Object;
    }

    [Fact]
    public void MarkProcessing_SetsStatusAndStartedAt()
    {
        JobExecution sut = CreatePendingJob();
        DateTime now = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        sut.MarkProcessing(MockTimeAt(now));

        sut.Status.ShouldBe(JobStatus.Processing);
        sut.StartedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkCompleted_SetsStatusProgressAndPayload()
    {
        JobExecution sut = CreatePendingJob();
        DateTime now = new(2026, 1, 1, 10, 5, 0, DateTimeKind.Utc);

        sut.MarkCompleted("result-payload", MockTimeAt(now));

        sut.Status.ShouldBe(JobStatus.Completed);
        sut.ProgressPercent.ShouldBe(100);
        sut.ResultPayload.ShouldBe("result-payload");
        sut.CompletedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkCompleted_WithNullPayload_SetsNullResultPayload()
    {
        JobExecution sut = CreatePendingJob();

        sut.MarkCompleted(null, MockTimeAt(DateTime.UtcNow));

        sut.ResultPayload.ShouldBeNull();
        sut.Status.ShouldBe(JobStatus.Completed);
    }

    [Fact]
    public void MarkFailed_SetsStatusErrorMessageAndCompletedAt()
    {
        JobExecution sut = CreatePendingJob();
        DateTime now = new(2026, 1, 1, 10, 10, 0, DateTimeKind.Utc);

        sut.MarkFailed("something went wrong", MockTimeAt(now));

        sut.Status.ShouldBe(JobStatus.Failed);
        sut.ErrorMessage.ShouldBe("something went wrong");
        sut.CompletedAtUtc.ShouldBe(now);
    }

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
