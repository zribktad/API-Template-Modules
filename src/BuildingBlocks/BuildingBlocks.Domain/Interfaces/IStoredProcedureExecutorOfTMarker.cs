namespace BuildingBlocks.Domain.Interfaces;

/// <summary>
///     Marker interface that discriminates <see cref="IStoredProcedureExecutor" /> registrations by module.
///     Domain handlers and repositories resolve this to get the executor bound to their module's DbContext.
/// </summary>
public interface IStoredProcedureExecutor<TMarker> : IStoredProcedureExecutor { }
