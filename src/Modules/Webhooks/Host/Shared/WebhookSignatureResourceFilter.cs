using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using SharedKernel.Application.Errors;
using Webhooks.Core.Shared;

namespace Webhooks.Host.Shared;

public sealed class WebhookSignatureResourceFilter : IAsyncResourceFilter
{
    private readonly IWebhookPayloadValidator _validator;
    private readonly IProblemDetailsService _problemDetailsService;

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

        request.EnableBuffering();
        using StreamReader reader = new(request.Body, leaveOpen: true);
        string body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        if (!_validator.IsValid(body, signature.ToString(), timestamp.ToString()))
        {
            await WriteUnauthorizedAsync(context, "Invalid webhook signature.");
            return;
        }

        await next();
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
