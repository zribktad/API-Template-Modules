using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Infrastructure;

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

    protected override async Task ProcessItemAsync(OutgoingWebhookItem item, CancellationToken ct)
    {
        WebhookSignatureResult signatureResult = _signer.Sign(item.SerializedPayload);

        using HttpClient client = _httpClientFactory.CreateClient(WebhookConstants.OutgoingHttpClientName);
        using HttpRequestMessage request = new(HttpMethod.Post, item.CallbackUrl)
        {
            Content = new StringContent(
                item.SerializedPayload,
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")
            ),
        };

        request.Headers.Add(WebhookConstants.SignatureHeader, signatureResult.Signature);
        request.Headers.Add(WebhookConstants.TimestampHeader, signatureResult.Timestamp);

        using HttpResponseMessage response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Outgoing webhook delivered to {Url}", item.CallbackUrl);
    }

    protected override Task HandleErrorAsync(OutgoingWebhookItem item, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Failed to deliver outgoing webhook to {Url}", item.CallbackUrl);
        return Task.CompletedTask;
    }
}
