using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using SharedKernel.Application.Errors;
using Webhooks.Contracts;

namespace Webhooks.Security;

public sealed class WebhookSignatureResourceFilter : IAsyncResourceFilter
{
    // Threshold above which ASP.NET Core spills the buffered request body to disk. 64 KB is the framework default.
    private const int BufferThresholdBytes = 64 * 1024;

    // Hardcoded limit matching [RequestSizeLimit(1024 * 1024)] on the controller.
    // Keep in sync with the controller attribute — see the note above EnableBuffering below
    // for why both layers exist.
    private const int MaxBodyBytes = 1024 * 1024;

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IWebhookPayloadValidator _validator;

    public WebhookSignatureResourceFilter(
        IWebhookPayloadValidator validator,
        IProblemDetailsService problemDetailsService
    )
    {
        _validator = validator;
        _problemDetailsService = problemDetailsService;
    }

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next
    )
    {
        bool hasAttribute = context.ActionDescriptor.EndpointMetadata.Any(m =>
            m is ValidateWebhookSignatureAttribute
        );

        if (!hasAttribute)
        {
            await next();
            return;
        }

        HttpRequest request = context.HttpContext.Request;

        if (
            !request.Headers.TryGetValue(
                WebhookConstants.SignatureHeader,
                out StringValues signature
            )
            || !request.Headers.TryGetValue(
                WebhookConstants.TimestampHeader,
                out StringValues timestamp
            )
        )
        {
            await WriteUnauthorizedAsync(context, "Missing required webhook signature headers.");
            return;
        }

        // Two-layer size defense (intentional):
        //   1) [RequestSizeLimit] on the controller — Kestrel rejects oversized bodies at the
        //      server level before any buffering happens (cheap, short-circuit, generic 413).
        //   2) bufferLimit here — authoritative application-level guard that measures the real
        //      stream regardless of the Content-Length header; protects this filter even if a
        //      future endpoint forgets the attribute or the attribute value drifts.
        // Both limits are kept identical on purpose: Kestrel normally wins first, this layer
        // exists as a safety net. No ProblemDetails is produced by the server layer — that's
        // accepted; the application layer responds via WritePayloadTooLargeAsync when reached.
        request.EnableBuffering(bufferThreshold: BufferThresholdBytes, bufferLimit: MaxBodyBytes);
        string body;
        try
        {
            using StreamReader reader = new(request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        }
        catch (InvalidDataException)
        {
            await WritePayloadTooLargeAsync(context, MaxBodyBytes);
            return;
        }
        request.Body.Position = 0;

        if (!_validator.IsValid(body, signature.ToString(), timestamp.ToString()))
        {
            await WriteUnauthorizedAsync(context, "Invalid webhook signature.");
            return;
        }

        await next();
    }

    private async Task WritePayloadTooLargeAsync(ResourceExecutingContext context, int maxBodyBytes)
    {
        ProblemDetails pd = new()
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload Too Large",
            Detail = $"Webhook payload exceeds the maximum allowed size of {maxBodyBytes} bytes.",
        };

        context.HttpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

        bool written = await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = pd }
        );

        context.Result = written
            ? new EmptyResult()
            : new ObjectResult(pd) { StatusCode = StatusCodes.Status413PayloadTooLarge };
    }

    private async Task WriteUnauthorizedAsync(ResourceExecutingContext context, string detail)
    {
        string errorCode = ErrorCatalog.Auth.Unauthorized;
        ProblemDetails pd = new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail,
        };
        pd.Extensions["errorCode"] = errorCode;

        context.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        bool written = await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = pd }
        );

        context.Result = written
            ? new EmptyResult()
            : new ObjectResult(pd) { StatusCode = StatusCodes.Status401Unauthorized };
    }
}
