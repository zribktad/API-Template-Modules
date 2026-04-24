using BackgroundJobs.Domain;
using BackgroundJobs.Features;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Application.Errors;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

[Trait("Category", "Unit")]
public sealed class SubmitJobCommandHandlerTests
{
    private readonly Mock<IJobQueue> _jobQueue = new();
    private readonly Mock<ILogger<SubmitJobCommandHandler>> _logger = new();
    private readonly DateTime _now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IJobExecutionRepository> _repository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUnitOfWork<BackgroundJobsDbMarker>> _unitOfWork = new();

    public SubmitJobCommandHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_now));
        _unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task>, CancellationToken, TransactionOptions?>(
                async (action, _, _) => await action()
            );
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

        ErrorOr<JobStatusResponse> result = await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("data-export")),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            _logger.Object,
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

        ErrorOr<JobStatusResponse> result = await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("report-gen", "param1")),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            _logger.Object,
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
            _logger.Object,
            ct
        );

        callOrder.ShouldBe(["add", "enqueue"]);
    }

    [Fact]
    public async Task HandleAsync_WhenEnqueueFails_DeletesEntityAndReturnsError()
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

        ErrorOr<JobStatusResponse> result = await SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(new SubmitJobRequest("report-gen")),
            _repository.Object,
            _jobQueue.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            _logger.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.General.Unknown);
        // The entity is removed rather than transitioned to Failed (Pending→Failed is illegal per state machine).
        persisted.ShouldNotBeNull();
        _repository.Verify(r => r.DeleteAsync(persisted!, ct), Times.Once);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<JobExecution>(), ct), Times.Never);
    }
}
