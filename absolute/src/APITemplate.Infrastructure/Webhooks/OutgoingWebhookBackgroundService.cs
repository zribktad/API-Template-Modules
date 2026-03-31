using System.Net.Http.Headers;
using System.Text;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// Background service that drains the outgoing webhook queue, signs each payload with HMAC-SHA256,
/// and delivers it via HTTP POST to the registered callback URL.
/// </summary>
public sealed class OutgoingWebhookBackgroundService
    : QueueConsumerBackgroundService<OutgoingWebhookItem>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookPayloadSigner _signer;
    private readonly ILogger<OutgoingWebhookBackgroundService> _logger;

    public OutgoingWebhookBackgroundService(
        IOutgoingWebhookQueueReader queue,
        IHttpClientFactory httpClientFactory,
        IWebhookPayloadSigner signer,
        ILogger<OutgoingWebhookBackgroundService> logger
    )
        : base(queue)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _logger = logger;
    }

    /// <summary>Signs and HTTP-POSTs the outgoing webhook item to its registered callback URL.</summary>
    protected override async Task ProcessItemAsync(OutgoingWebhookItem item, CancellationToken ct)
    {
        var signatureResult = _signer.Sign(item.SerializedPayload);

        using var client = _httpClientFactory.CreateClient(WebhookConstants.OutgoingHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, item.CallbackUrl)
        {
            Content = new StringContent(
                item.SerializedPayload,
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")
            ),
        };

        request.Headers.Add(WebhookConstants.SignatureHeader, signatureResult.Signature);
        request.Headers.Add(WebhookConstants.TimestampHeader, signatureResult.Timestamp);

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Outgoing webhook delivered to {Url}", item.CallbackUrl);
    }

    /// <summary>Logs delivery failures at error level and returns a completed task to allow the queue to continue processing.</summary>
    protected override Task HandleErrorAsync(
        OutgoingWebhookItem item,
        Exception ex,
        CancellationToken ct
    )
    {
        _logger.LogError(ex, "Failed to deliver outgoing webhook to {Url}", item.CallbackUrl);
        return Task.CompletedTask;
    }
}
