using BackgroundJobs.Application.Features.Jobs;
using BackgroundJobs.Application.Features.Jobs.DTOs;
using BackgroundJobs.Application.Services;
using BackgroundJobs.Domain;
using Moq;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

public sealed class SubmitJobCommandHandlerTests
{
    private readonly Mock<IJobExecutionRepository> _repository = new();
    private readonly Mock<IJobQueue> _jobQueue = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly DateTime _now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    public SubmitJobCommandHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_now));
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<SharedKernel.Domain.Options.TransactionOptions?>()))
            .Returns<Func<Task>, CancellationToken, SharedKernel.Domain.Options.TransactionOptions?>(async (action, _, _) => await action());
    }

    [Fact]
    public async Task HandleAsync_PersistsEntityAndEnqueuesId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid enqueuedId = Guid.Empty;
        _jobQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), ct))
            .Callback<Guid, CancellationToken>((id, _) => enqueuedId = id)
            .Returns(ValueTask.CompletedTask);

        ErrorOr.ErrorOr<JobStatusResponse> result = await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("data-export")),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        _repository.Verify(r => r.AddAsync(It.IsAny<JobExecution>(), ct), Times.Once);
        enqueuedId.ShouldBe(result.Value.Id);
    }

    [Fact]
    public async Task HandleAsync_ReturnsPendingResponseWithCorrectFields()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _jobQueue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), ct)).Returns(ValueTask.CompletedTask);

        ErrorOr.ErrorOr<JobStatusResponse> result = await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("report-gen", "param1", null)),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.JobType.ShouldBe("report-gen");
        result.Value.Status.ShouldBe(JobStatus.Pending);
        result.Value.Parameters.ShouldBe("param1");
        result.Value.SubmittedAtUtc.ShouldBe(_now);
    }

    [Fact]
    public async Task HandleAsync_EnqueuesAfterPersist()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> callOrder = [];
        _repository
            .Setup(r => r.AddAsync(It.IsAny<JobExecution>(), ct))
            .Callback<JobExecution, CancellationToken>((_, _) => callOrder.Add("add"))
            .ReturnsAsync((JobExecution e, CancellationToken _) => e);
        _jobQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), ct))
            .Callback(() => callOrder.Add("enqueue"))
            .Returns(ValueTask.CompletedTask);

        await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("test")),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            ct
        );

        callOrder.ShouldBe(["add", "enqueue"]);
    }

    [Fact]
    public async Task HandleAsync_WhenEnqueueFails_MarksJobAsFailedAndReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobExecution? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<JobExecution>(), ct))
            .Callback<JobExecution, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((JobExecution entity, CancellationToken _) => entity);
        _jobQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), ct))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        ErrorOr.ErrorOr<JobStatusResponse> result = await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("report-gen")),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(SharedKernel.Application.Errors.ErrorCatalog.General.Unknown);
        persisted.ShouldNotBeNull();
        persisted!.Status.ShouldBe(JobStatus.Failed);
        persisted.ErrorMessage.ShouldNotBeNull();
        persisted.ErrorMessage.ShouldContain("queue unavailable");
        _repository.Verify(r => r.UpdateAsync(persisted, ct), Times.Once);
    }
}
