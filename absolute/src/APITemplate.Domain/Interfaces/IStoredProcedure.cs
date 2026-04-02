namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Represents a single stored procedure call.
/// Each stored procedure is its own sealed record that owns:
///   - the SQL template (the function name)
///   - the parameter values (as constructor properties)
///   - the result type (via the generic parameter)
///
/// Usage example:
/// <code>
///   var proc   = new GetProductCategoryStatsProcedure(categoryId);
///   var result = await _executor.QueryFirstAsync(proc, ct);
/// </code>
/// </summary>
/// <typeparam name="TResult">
/// The keyless entity type that EF Core will materialise from the procedure result set.
/// Must be registered with HasNoKey() in the DbContext.
/// </typeparam>
public interface IStoredProcedure<TResult> where TResult : class
{
    /// <summary>
    /// Returns an interpolated SQL string with all parameter values embedded.
    /// EF Core automatically converts each interpolated value into a named
    /// SQL parameter (@p0, @p1, ...), preventing SQL injection.
    /// </summary>
    FormattableString ToSql();
}
