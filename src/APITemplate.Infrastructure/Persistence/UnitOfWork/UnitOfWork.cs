using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Legacy namespace wrapper for the SharedKernel generic unit-of-work implementation.
/// </summary>
public sealed class UnitOfWork
    : SharedKernel.Infrastructure.UnitOfWork.UnitOfWork<AppDbContext>,
        IUnitOfWork,
        IUnitOfWork<AppDbContext>
{
    public UnitOfWork(
        AppDbContext dbContext,
        IOptions<TransactionDefaultsOptions> transactionDefaults,
        ILogger<UnitOfWork> logger,
        IDbTransactionProvider transactionProvider)
        : base(dbContext, transactionDefaults, logger, transactionProvider) { }
}
