using BackgroundJobs.Domain;
using BackgroundJobs.Features;
using ErrorOr;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

[Trait("Category", "Unit")]
public sealed class GetJobStatusQueryHandlerTests
{
    private readonly Mock<IJobExecutionRepository> _repository = new();

    [Fact]
    public async Task HandleAsync_WhenEntityNotFound_ReturnsNotFoundError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, ct)).ReturnsAsync((JobExecution?)null);

        ErrorOr<JobStatusResponse> result = await GetJobStatusQueryHandler.HandleAsync(
            new GetJobStatusQuery(new GetJobStatusRequest(id)),
            _repository.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenEntityExists_ReturnsMappedResponse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobExecution entity = new()
        {
            Id = Guid.NewGuid(),
            JobType = "data-export",
            SubmittedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _repository.Setup(r => r.GetByIdAsync(entity.Id, ct)).ReturnsAsync(entity);

        ErrorOr<JobStatusResponse> result = await GetJobStatusQueryHandler.HandleAsync(
            new GetJobStatusQuery(new GetJobStatusRequest(entity.Id)),
            _repository.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(entity.Id);
        result.Value.JobType.ShouldBe("data-export");
        result.Value.Status.ShouldBe(JobStatus.Pending);
    }
}
