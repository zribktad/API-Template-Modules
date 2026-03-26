using System.Diagnostics;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Wraps stored-procedure and raw SQL executions in a diagnostic activity,
/// recording the operation name, command name, result count, and any exception type.
/// </summary>
public static class StoredProcedureTelemetry
{
    private static readonly ActivitySource ActivitySource = new(
        ObservabilityConventions.ActivitySourceName
    );

    /// <summary>Executes <paramref name="action"/> inside a traced activity for a single-result stored procedure query.</summary>
    public static Task<TResult?> TraceQueryFirstAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        Func<Task<TResult?>> action
    )
        where TResult : class =>
        TraceAsync(
            TelemetryStoredProcedureOperations.QueryFirst,
            procedure.GetType().Name,
            action,
            result => result is null ? 0 : 1
        );

    /// <summary>Executes <paramref name="action"/> inside a traced activity for a multi-result stored procedure query.</summary>
    public static Task<IReadOnlyList<TResult>> TraceQueryManyAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        Func<Task<IReadOnlyList<TResult>>> action
    )
        where TResult : class =>
        TraceAsync(
            TelemetryStoredProcedureOperations.QueryMany,
            procedure.GetType().Name,
            action,
            results => results.Count
        );

    /// <summary>Executes <paramref name="action"/> inside a traced activity for a raw SQL execute operation.</summary>
    public static Task<int> TraceExecuteAsync(FormattableString sql, Func<Task<int>> action) =>
        TraceAsync(
            TelemetryStoredProcedureOperations.Execute,
            ResolveSqlOperationName(sql),
            action,
            affectedRows => affectedRows
        );

    private static async Task<TResult> TraceAsync<TResult>(
        string operation,
        string commandName,
        Func<Task<TResult>> action,
        Func<TResult, int> resultCountSelector
    )
    {
        using var activity = ActivitySource.StartActivity(
            TelemetryActivityNames.StoredProcedure(operation),
            ActivityKind.Internal
        );
        activity?.SetTag(TelemetryTagKeys.DbOperation, operation);
        activity?.SetTag(TelemetryTagKeys.DbStoredProcedureName, commandName);
        try
        {
            var result = await action();
            activity?.SetTag(TelemetryTagKeys.DbResultCount, resultCountSelector(result));
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(TelemetryTagKeys.ExceptionType, ex.GetType().Name);
            throw;
        }
    }

    private static string ResolveSqlOperationName(FormattableString sql)
    {
        var commandText = sql.Format.TrimStart();
        var firstToken = commandText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstToken)
            ? TelemetryDefaults.Sql
            : firstToken.ToLowerInvariant();
    }
}
