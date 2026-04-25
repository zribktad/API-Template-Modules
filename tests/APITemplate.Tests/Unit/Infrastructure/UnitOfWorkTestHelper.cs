using Moq;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;

namespace APITemplate.Tests.Unit.Infrastructure;

internal static class UnitOfWorkTestHelper
{
    public static void SetupTransactionPassthrough<TMarker>(Mock<IUnitOfWork<TMarker>> unitOfWork)
    {
        unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task>, CancellationToken, TransactionOptions?>(
                static (action, _, _) => action()
            );
    }

    public static void SetupTransactionPassthrough<TMarker, TResult>(
        Mock<IUnitOfWork<TMarker>> unitOfWork
    )
    {
        unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<TResult>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task<TResult>>, CancellationToken, TransactionOptions?>(
                static (action, _, _) => action()
            );
    }

    public static void SetupTransactionPassthrough(Mock<IUnitOfWork> unitOfWork)
    {
        unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task>, CancellationToken, TransactionOptions?>(
                static (action, _, _) => action()
            );
    }

    public static void SetupTransactionFailure<TMarker>(
        Mock<IUnitOfWork<TMarker>> unitOfWork,
        Exception exception
    )
    {
        unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .ThrowsAsync(exception);
    }
}
