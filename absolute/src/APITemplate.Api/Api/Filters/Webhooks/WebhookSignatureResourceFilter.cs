using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.Errors;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace APITemplate.Api.Filters.Webhooks;

/// <summary>
/// Resource filter that validates the HMAC signature of incoming webhook requests for
/// actions decorated with <see cref="ValidateWebhookSignatureAttribute"/>.
/// Reads the raw request body (with buffering enabled) and delegates to <see cref="IWebhookPayloadValidator"/>.
/// Throws <see cref="UnauthorizedException"/> if required headers are absent or the signature is invalid.
/// </summary>
public sealed class WebhookSignatureResourceFilter : IAsyncResourceFilter
{
    private readonly IWebhookPayloadValidator _validator;

    public WebhookSignatureResourceFilter(IWebhookPayloadValidator validator)
    {
        _validator = validator;
    }

    /// <summary>
    /// Validates the webhook signature before the action executes; passes through unchanged
    /// if the endpoint does not carry <see cref="ValidateWebhookSignatureAttribute"/>.
    /// </summary>
    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next
    )
    {
        var hasAttribute = context.ActionDescriptor.EndpointMetadata.Any(m =>
            m is ValidateWebhookSignatureAttribute
        );

        if (!hasAttribute)
        {
            await next();
            return;
        }

        var request = context.HttpContext.Request;

        if (
            !request.Headers.TryGetValue(WebhookConstants.SignatureHeader, out var signature)
            || !request.Headers.TryGetValue(WebhookConstants.TimestampHeader, out var timestamp)
        )
        {
            throw new UnauthorizedException(
                "Missing required webhook signature headers.",
                ErrorCatalog.Examples.WebhookMissingHeaders
            );
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        if (!_validator.IsValid(body, signature.ToString(), timestamp.ToString()))
        {
            throw new UnauthorizedException(
                "Invalid webhook signature.",
                ErrorCatalog.Examples.WebhookInvalidSignature
            );
        }

        await next();
    }
}
