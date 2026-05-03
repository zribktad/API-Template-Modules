using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using BuildingBlocks.Application.Contracts;

namespace BuildingBlocks.Web.Api.Filters.Idempotency;

/// <summary>
///     Filter that enforces idempotency for endpoints decorated with <see cref="IdempotentAttribute" />.
///     On the first call the response is stored in <see cref="IIdempotencyStore" />; subsequent calls with
///     the same <c>Idempotency-Key</c> header replay the cached response without re-executing the action.
///     Implements both <see cref="IAsyncActionFilter" /> (key validation, lock acquisition) and
///     <see cref="IAsyncResultFilter" /> (post-execution caching with accurate headers).
/// </summary>
public sealed class IdempotencyActionFilter : IAsyncActionFilter, IAsyncResultFilter
{
    private const string ContextKey = "__IdempotencyContext";
    private readonly JsonSerializerOptions _jsonOptions;

    private readonly IIdempotencyStore _store;

    public IdempotencyActionFilter(IIdempotencyStore store, IOptions<JsonOptions> jsonOptions)
    {
        _store = store;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        IdempotentAttribute? attribute = context
            .ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            await next();
            return;
        }

        if (
            !context.HttpContext.Request.Headers.TryGetValue(
                IdempotencyConstants.HeaderName,
                out StringValues keyValues
            ) || string.IsNullOrWhiteSpace(keyValues)
        )
        {
            context.Result = new BadRequestObjectResult(
                "Idempotency-Key header is required for this endpoint."
            );
            return;
        }

        string key = keyValues.ToString();
        if (key.Length > IdempotencyConstants.MaxKeyLength)
        {
            context.Result = new BadRequestObjectResult(
                $"Idempotency key must not exceed {IdempotencyConstants.MaxKeyLength} characters."
            );
            return;
        }

        TimeSpan resultTtl = TimeSpan.FromHours(attribute.TtlHours);
        TimeSpan lockTimeout = TimeSpan.FromSeconds(attribute.LockTimeoutSeconds);
        CancellationToken ct = context.HttpContext.RequestAborted;

        IdempotencyCacheEntry? existing = await _store.TryGetAsync(key, ct);
        if (existing is not null)
        {
            if (existing.LocationHeader is not null)
                context.HttpContext.Response.Headers.Location = existing.LocationHeader;

            context.Result = new ContentResult
            {
                StatusCode = existing.StatusCode,
                Content = existing.ResponseBody,
                ContentType = existing.ResponseContentType,
            };
            return;
        }

        string? lockToken = await _store.TryAcquireAsync(key, lockTimeout, ct);
        if (lockToken is null)
        {
            context.Result = new ConflictObjectResult(
                "A request with this idempotency key is already being processed."
            );
            return;
        }

        ActionExecutedContext executedContext;
        try
        {
            executedContext = await next();
        }
        catch
        {
            await _store.ReleaseAsync(key, lockToken, ct);
            throw;
        }

        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            await _store.ReleaseAsync(key, lockToken, ct);
            return;
        }

        int? statusCode = executedContext.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => null,
        };

        if (statusCode is >= 200 and < 300)
        {
            context.HttpContext.Items[ContextKey] = new IdempotencyResultContext(
                key,
                lockToken,
                resultTtl,
                executedContext.Result!
            );
        }
        else
            await _store.ReleaseAsync(key, lockToken, ct);
    }

    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next
    )
    {
        if (
            !context.HttpContext.Items.Remove(ContextKey, out object? raw)
            || raw is not IdempotencyResultContext ctx
        )
        {
            await next();
            return;
        }

        CancellationToken ct = context.HttpContext.RequestAborted;
        try
        {
            await next();

            string? responseBody = null;
            if (ctx.Result is ObjectResult objectResult && objectResult.Value is not null)
                responseBody = JsonSerializer.Serialize(objectResult.Value, _jsonOptions);

            string? contentType = context.HttpContext.Response.ContentType;
            string? locationHeader = context.HttpContext.Response.Headers.Location;
            int statusCode = context.HttpContext.Response.StatusCode;

            IdempotencyCacheEntry entry = new(
                statusCode,
                responseBody,
                contentType,
                locationHeader
            );
            await _store.SetAsync(ctx.Key, entry, ctx.Ttl, ct);
        }
        finally
        {
            await _store.ReleaseAsync(ctx.Key, ctx.LockToken, ct);
        }
    }

    private sealed record IdempotencyResultContext(
        string Key,
        string LockToken,
        TimeSpan Ttl,
        IActionResult Result
    );
}

