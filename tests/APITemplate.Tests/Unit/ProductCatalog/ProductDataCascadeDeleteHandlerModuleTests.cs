using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using ProductCatalog.Features.ProductData.DeleteProductData;
using ProductCatalog.Interfaces;
using SharedKernel.Contracts.Events;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductDataCascadeDeleteHandlerModuleTests
{
    private readonly Mock<IProductDataRepository> _repositoryMock = new();
    private readonly Mock<IMongoProductDataDeletePipelineProvider> _pipelineProviderMock = new();
    private readonly Mock<ILogger<ProductDataCascadeDeleteHandler>> _loggerMock = new();

    public ProductDataCascadeDeleteHandlerModuleTests()
    {
        _pipelineProviderMock.Setup(p => p.Get()).Returns(ResiliencePipeline.Empty);
        _loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantSoftDeleted_CallsSoftDeleteByTenantAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        DateTime deletedAt = DateTime.UtcNow;
        TenantSoftDeletedNotification notification = new(tenantId, actorId, deletedAt);

        _repositoryMock
            .Setup(r => r.SoftDeleteByTenantAsync(tenantId, actorId, deletedAt, ct))
            .ReturnsAsync(5);

        await ProductDataCascadeDeleteHandler.HandleAsync(
            notification,
            _repositoryMock.Object,
            _pipelineProviderMock.Object,
            _loggerMock.Object,
            ct
        );

        _repositoryMock.Verify(
            r => r.SoftDeleteByTenantAsync(tenantId, actorId, deletedAt, ct),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryThrows_RethrowsForWolverineRetry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TenantSoftDeletedNotification notification = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow
        );

        _repositoryMock
            .Setup(r =>
                r.SoftDeleteByTenantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .ThrowsAsync(new InvalidOperationException("MongoDB connection failed"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ProductDataCascadeDeleteHandler.HandleAsync(
                notification,
                _repositoryMock.Object,
                _pipelineProviderMock.Object,
                _loggerMock.Object,
                ct
            )
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryThrows_LogsErrorBeforeRethrowing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TenantSoftDeletedNotification notification = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow
        );
        InvalidOperationException ex = new("MongoDB down");

        _repositoryMock
            .Setup(r =>
                r.SoftDeleteByTenantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .ThrowsAsync(ex);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ProductDataCascadeDeleteHandler.HandleAsync(
                notification,
                _repositoryMock.Object,
                _pipelineProviderMock.Object,
                _loggerMock.Object,
                ct
            )
        );

        // EventId 4002 = ProductDataCascadeDeleteFailed (see ProductCatalogLogs.cs)
        _loggerMock.Verify(
            l =>
                l.Log(
                    LogLevel.Error,
                    It.Is<EventId>(e => e.Id == 4002),
                    It.IsAny<It.IsAnyType>(),
                    ex,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSuccessful_LogsInformation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TenantSoftDeletedNotification notification = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow
        );

        _repositoryMock
            .Setup(r =>
                r.SoftDeleteByTenantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .ReturnsAsync(7);

        await ProductDataCascadeDeleteHandler.HandleAsync(
            notification,
            _repositoryMock.Object,
            _pipelineProviderMock.Object,
            _loggerMock.Object,
            ct
        );

        // EventId 4001 = ProductDataCascadeDeleteSucceeded (see ProductCatalogLogs.cs)
        _loggerMock.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.Is<EventId>(e => e.Id == 4001),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
