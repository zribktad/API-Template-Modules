using BackgroundJobs.Domain;
using BackgroundJobs.Features;
using BuildingBlocks.Domain.Interfaces;
using BuildingBlocks.Domain.Options;
using ErrorOr;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

[Trait("Category", "Unit")]
public sealed class SubmitJobCommandHandlerTests
{
    private readonly Mock<IIdGenerator> _idGenerator = new();
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

    private Task<(ErrorOr<JobStatusResponse>, OutgoingMessages)> InvokeAsync(
        SubmitJobRequest request,
        CancellationToken ct
    ) =>
        SubmitJobCommandHandler.HandleAsync(
            new SubmitJobCommand(request),
            _repository.Object,
            _unitOfWork.Object,
            _timeProvider.Object,
            _idGenerator.Object,
            ct
        );

    [Fact]
    public async Task HandleAsync_UsesInjectedIdGeneratorForPersistedJobId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid generatedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        JobExecution? persisted = null;
        _idGenerator.Setup(g => g.NewId()).Returns(generatedId);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<JobExecution>(), ct))
            .Callback<JobExecution, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((JobExecution entity, CancellationToken _) => entity);

        (ErrorOr<JobStatusResponse> result, OutgoingMessages _) = await InvokeAsync(
            new SubmitJobRequest("data-export"),
            ct
        );

        result.IsError.ShouldBeFalse();
        persisted.ShouldNotBeNull();
        persisted!.Id.ShouldBe(generatedId);
        result.Value.Id.ShouldBe(generatedId);
    }

    [Fact]
    public async Task HandleAsync_PersistsEntityAndCascadesProcessJobCommand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (ErrorOr<JobStatusResponse> result, OutgoingMessages messages) = await InvokeAsync(
            new SubmitJobRequest("data-export"),
            ct
        );

        result.IsError.ShouldBeFalse();
        _repository.Verify(r => r.AddAsync(It.IsAny<JobExecution>(), ct), Times.Once);

        ProcessJobCommand cascade = messages.OfType<ProcessJobCommand>().ShouldHaveSingleItem();
        cascade.JobId.ShouldBe(result.Value.Id);
    }

    [Fact]
    public async Task HandleAsync_ReturnsPendingResponseWithCorrectFields()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (ErrorOr<JobStatusResponse> result, OutgoingMessages _) = await InvokeAsync(
            new SubmitJobRequest("report-gen", "param1"),
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.JobType.ShouldBe("report-gen");
        result.Value.Status.ShouldBe(JobStatus.Pending);
        result.Value.Parameters.ShouldBe("param1");
        result.Value.SubmittedAtUtc.ShouldBe(_now);
    }
}
