using System.Diagnostics;
using APITemplate.Infrastructure.Observability;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Resolvers;

namespace APITemplate.Api.GraphQL.Instrumentation;

/// <summary>
/// Hot Chocolate diagnostic event listener that records OpenTelemetry metrics for every
/// GraphQL request lifecycle event (duration, errors, cache hits/misses, resolver errors, cost).
/// </summary>
public sealed class GraphQlExecutionMetricsListener : ExecutionDiagnosticEventListener
{
    /// <summary>
    /// Starts timing the request and records duration and error status on completion
    /// via the returned <see cref="IDisposable"/> scope.
    /// </summary>
    public override IDisposable ExecuteRequest(IRequestContext context)
    {
        var operationType = GetOperationType(context);
        var startedAt = Stopwatch.GetTimestamp();

        return Scope.Create(() =>
        {
            var hasErrors =
                context.Result is IOperationResult operationResult
                && operationResult.Errors is { Count: > 0 };
            GraphQlTelemetry.RecordRequest(
                operationType,
                hasErrors,
                Stopwatch.GetElapsedTime(startedAt)
            );
        });
    }

    /// <summary>Records an unhandled request-level exception metric.</summary>
    public override void RequestError(IRequestContext context, Exception exception) =>
        GraphQlTelemetry.RecordRequestError();

    /// <summary>Records a GraphQL syntax parse error metric.</summary>
    public override void SyntaxError(IRequestContext context, IError error) =>
        GraphQlTelemetry.RecordSyntaxError();

    /// <summary>Records one validation error metric per error in the list.</summary>
    public override void ValidationErrors(IRequestContext context, IReadOnlyList<IError> errors)
    {
        for (var i = 0; i < errors.Count; i++)
        {
            GraphQlTelemetry.RecordValidationError();
        }
    }

    /// <summary>Records a field-level resolver error metric.</summary>
    public override void ResolverError(IMiddlewareContext context, IError error) =>
        GraphQlTelemetry.RecordResolverError();

    /// <summary>Records a document cache miss (document parsed and stored).</summary>
    public override void AddedDocumentToCache(IRequestContext context) =>
        GraphQlTelemetry.RecordDocumentCacheMiss();

    /// <summary>Records a document cache hit (document retrieved without re-parsing).</summary>
    public override void RetrievedDocumentFromCache(IRequestContext context) =>
        GraphQlTelemetry.RecordDocumentCacheHit();

    /// <summary>Records an operation cache miss (operation plan stored).</summary>
    public override void AddedOperationToCache(IRequestContext context) =>
        GraphQlTelemetry.RecordOperationCacheMiss();

    /// <summary>Records an operation cache hit (operation plan reused).</summary>
    public override void RetrievedOperationFromCache(IRequestContext context) =>
        GraphQlTelemetry.RecordOperationCacheHit();

    /// <summary>Records field and type cost metrics for the completed operation.</summary>
    public override void OperationCost(
        IRequestContext context,
        double fieldCost,
        double typeCost
    ) => GraphQlTelemetry.RecordOperationCost(fieldCost, typeCost);

    private static string GetOperationType(IRequestContext context) =>
        context.Operation?.Type.ToString().ToLowerInvariant() ?? TelemetryDefaults.Unknown;

    /// <summary>
    /// Single-use scope that executes a callback exactly once on disposal, guarded by
    /// an interlocked flag to prevent double-recording in concurrent scenarios.
    /// </summary>
    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;

        private Scope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        /// <summary>Creates a new <see cref="Scope"/> that invokes <paramref name="onDispose"/> when disposed.</summary>
        public static Scope Create(Action onDispose) => new(onDispose);

        /// <summary>Invokes the dispose callback exactly once using an interlocked exchange.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _onDispose();
            }
        }
    }
}
