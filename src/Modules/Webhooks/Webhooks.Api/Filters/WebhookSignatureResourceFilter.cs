using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using SharedKernel.Domain.Exceptions;
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
        bool hasAttribute = context.ActionDescriptor.EndpointMetadata
            .Any(m => m is ValidateWebhookSignatureAttribute);

        if (!hasAttribute)
        {
            await next();
            return;
        }

        HttpRequest request = context.HttpContext.Request;

        if (!request.Headers.TryGetValue(WebhookConstants.SignatureHeader, out StringValues signature)
            || !request.Headers.TryGetValue(WebhookConstants.TimestampHeader, out StringValues timestamp))
        {
            throw new UnauthorizedException("Missing required webhook signature headers.");
        }

        request.EnableBuffering();
        using StreamReader reader = new(request.Body, leaveOpen: true);
        string body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        if (!_validator.IsValid(body, signature.ToString(), timestamp.ToString()))
        {
            throw new UnauthorizedException("Invalid webhook signature.");
        }

        await next();
    }
}
