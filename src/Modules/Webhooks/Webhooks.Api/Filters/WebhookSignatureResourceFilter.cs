using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Api.Filters;

public sealed class WebhookSignatureResourceFilter : IAsyncResourceFilter
{
    private readonly IWebhookPayloadValidator _validator;

    public WebhookSignatureResourceFilter(IWebhookPayloadValidator validator)
    {
        _validator = validator;
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
            context.Result = new ObjectResult(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Missing required webhook signature headers.",
                }
            )
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
            return;
        }

        request.EnableBuffering();
        using StreamReader reader = new(request.Body, leaveOpen: true);
        string body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        if (!_validator.IsValid(body, signature.ToString(), timestamp.ToString()))
        {
            context.Result = new ObjectResult(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Invalid webhook signature.",
                }
            )
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
            return;
        }

        await next();
    }
}
