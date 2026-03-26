using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using Moq;

namespace APITemplate.Tests.Unit.Handlers;

internal static class UnitOfWorkMockExtensions
{
    public static void SetupImmediateTransactionExecution(this Mock<IUnitOfWork> unitOfWorkMock)
    {
        unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()))
            .Returns((Func<Task> action, CancellationToken _, TransactionOptions? _) => action());
    }

    public static void SetupImmediateTransactionExecution<T>(this Mock<IUnitOfWork> unitOfWorkMock)
    {
        unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<T>>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()))
            .Returns((Func<Task<T>> action, CancellationToken _, TransactionOptions? _) => action());
    }
}
