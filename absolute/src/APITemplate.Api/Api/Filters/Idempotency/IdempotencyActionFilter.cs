using System.Net.Mime;
using System.Text.Json;
using APITemplate.Application.Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace APITemplate.Api.Filters.Idempotency;

/// <summary>
/// Action filter that enforces idempotency for endpoints decorated with <see cref="IdempotentAttribute"/>.
/// On the first call the response is stored in <see cref="IIdempotencyStore"/>; subsequent calls with
/// the same <c>Idempotency-Key</c> header replay the cached response without re-executing the action.
/// </summary>
public sealed class IdempotencyActionFilter : IAsyncActionFilter
{
    private readonly IIdempotencyStore _store;

    public IdempotencyActionFilter(IIdempotencyStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Intercepts the action execution to check for a cached idempotent result or to store
    /// a new one, ensuring at-most-once semantics for the decorated endpoint.
    /// </summary>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var attribute = context
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
                out var keyValues
            ) || string.IsNullOrWhiteSpace(keyValues)
        )
        {
            context.Result = new BadRequestObjectResult(
                "Idempotency-Key header is required for this endpoint."
            );
            return;
        }

        var key = keyValues.ToString();
        if (key.Length > IdempotencyConstants.MaxKeyLength)
        {
            context.Result = new BadRequestObjectResult(
                $"Idempotency key must not exceed {IdempotencyConstants.MaxKeyLength} characters."
            );
            return;
        }

        var resultTtl = TimeSpan.FromHours(attribute.TtlHours);
        var lockTimeout = TimeSpan.FromSeconds(attribute.LockTimeoutSeconds);
        var ct = context.HttpContext.RequestAborted;

        var existing = await _store.TryGetAsync(key, ct);
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

        if (!await _store.TryAcquireAsync(key, lockTimeout, ct))
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
            await _store.ReleaseAsync(key, ct);
            throw;
        }

        if (
            executedContext.Result is ObjectResult objectResult
            && objectResult.StatusCode is >= 200 and < 300
        )
        {
            var responseBody = objectResult.Value is not null
                ? JsonSerializer.Serialize(objectResult.Value, JsonSerializerOptions.Web)
                : null;

            string? locationHeader = executedContext.Result switch
            {
                CreatedResult cr => cr.Location,
                _ => null,
            };

            var entry = new IdempotencyCacheEntry(
                objectResult.StatusCode ?? 200,
                responseBody,
                MediaTypeNames.Application.Json,
                locationHeader
            );

            await _store.SetAsync(key, entry, resultTtl, ct);
        }

        await _store.ReleaseAsync(key, ct);
    }
}
