namespace SharedKernel.Domain.Interfaces;

/// <summary>
///     Marker interface that scopes a unit of work to a specific module's persistence boundary.
///     Modules use this to request the correct transactional boundary for their own persistence context.
///     The type parameter serves as a discriminator for DI resolution — it does not need to be a DbContext.
///     Domain layers define a simple marker type; Infrastructure maps it to the real DbContext.
/// </summary>
/// <typeparam name="TContext">
///     Marker type identifying the module's persistence boundary.
///     At the Infrastructure level this is typically a <c>DbContext</c>, but Domain-layer code
///     can use a plain marker class to avoid referencing EF Core directly.
/// </typeparam>
public interface IUnitOfWork<TContext> : IUnitOfWork;
