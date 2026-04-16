using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SharedKernel.Application.Errors;
using Webhooks.Contracts;

namespace Webhooks.Security;

public sealed class WebhookSignatureResourceFilter : IAsyncResourceFilter
{
    // Threshold above which ASP.NET Core spills the buffered request body to disk. 64 KB is the framework default.
    private const int BufferThresholdBytes = 64 * 1024;

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IWebhookPayloadValidator _validator;
    private readonly IOptions<WebhookOptions> _options;

    public WebhookSignatureResourceFilter(
        IWebhookPayloadValidator validator,
        IProblemDetailsService problemDetailsService,
        IOptions<WebhookOptions> options
    )
    {
        _validator = validator;
        _problemDetailsService = problemDetailsService;
        _options = options;
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
        int maxBodyBytes = _options.Value.MaxBodyBytes;

        // Fail fast on Content-Length to avoid buffering oversized payloads.
        if (request.ContentLength is long declared && declared > maxBodyBytes)
        {
            await WritePayloadTooLargeAsync(context, maxBodyBytes);
            return;
        }

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

        // bufferLimit throws InvalidDataException when body exceeds the limit during read.
        request.EnableBuffering(bufferThreshold: BufferThresholdBytes, bufferLimit: maxBodyBytes);
        string body;
        try
        {
            using StreamReader reader = new(request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        }
        catch (InvalidDataException)
        {
            await WritePayloadTooLargeAsync(context, maxBodyBytes);
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
