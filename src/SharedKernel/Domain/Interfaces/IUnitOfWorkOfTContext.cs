using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Domain.Interfaces;

/// <summary>
/// Marker interface that scopes a unit of work to a specific EF Core <see cref="DbContext"/>.
/// Modules use this to request the correct transactional boundary for their own persistence context.
/// </summary>
/// <typeparam name="TContext">Concrete EF Core DbContext owned by a module.</typeparam>
public interface IUnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext;
