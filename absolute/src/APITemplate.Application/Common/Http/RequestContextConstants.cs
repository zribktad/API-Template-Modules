namespace APITemplate.Application.Common.Http;

/// <summary>
/// Constants for request context headers and log enrichment properties.
/// </summary>
public static class RequestContextConstants
{
    public static class Headers
    {
        /// <summary>
        /// Header name used for correlation/trace IDs supplied by the caller.
        /// </summary>
        public const string CorrelationId = "X-Correlation-Id";

        /// <summary>
        /// Header name used for the distributed trace ID.
        /// </summary>
        public const string TraceId = "X-Trace-Id";

        /// <summary>
        /// Header name used for the request elapsed time in milliseconds.
        /// </summary>
        public const string ElapsedMs = "X-Elapsed-Ms";
    }

    public static class ContextKeys
    {
        /// <summary>
        /// Key under which the resolved correlation ID is stored in <see cref="AspNetCore.Http.HttpContext.Items"/>.
        /// </summary>
        public const string CorrelationId = "CorrelationId";
    }

    public static class LogProperties
    {
        /// <summary>
        /// Serilog property name for the correlation ID.
        /// </summary>
        public const string CorrelationId = "CorrelationId";

        /// <summary>
        /// Serilog property name for the tenant ID.
        /// </summary>
        public const string TenantId = "TenantId";
    }
}
