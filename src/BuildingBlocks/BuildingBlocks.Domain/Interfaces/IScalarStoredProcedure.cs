namespace BuildingBlocks.Domain.Interfaces;

/// <summary>
///     Represents a stored procedure that returns a scalar value (e.g. <see langword="string" />,
///     <see langword="int" />, <see langword="double" />).
///     <para>
///         Unlike <see cref="IStoredProcedure{TResult}" />, this interface has no <c>class</c> constraint,
///         allowing value-type results. Executed via <c>Database.SqlQuery&lt;T&gt;</c> instead of
///         <c>Set&lt;T&gt;().FromSqlInterpolated</c>.
///     </para>
/// </summary>
/// <typeparam name="TResult">The scalar type returned by the procedure.</typeparam>
public interface IScalarStoredProcedure<TResult>
{
    /// <inheritdoc cref="IStoredProcedure{TResult}.ToSql" />
    FormattableString ToSql();
}
