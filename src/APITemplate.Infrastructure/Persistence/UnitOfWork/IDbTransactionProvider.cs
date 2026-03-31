namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Legacy namespace shim for the SharedKernel transaction provider contract.
/// </summary>
public interface IDbTransactionProvider
    : SharedKernel.Infrastructure.UnitOfWork.IDbTransactionProvider;
