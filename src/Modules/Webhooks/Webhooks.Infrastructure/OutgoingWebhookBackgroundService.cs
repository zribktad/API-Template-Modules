using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Infrastructure;

public sealed class OutgoingWebhookBackgroundService
    : QueueConsumerBackgroundService<OutgoingWebhookItem>
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https",
        "http",
    };

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
        await ValidateCallbackUrlAsync(item.CallbackUrl, ct);

        WebhookSignatureResult signatureResult = _signer.Sign(item.SerializedPayload);

        using HttpClient client = _httpClientFactory.CreateClient(
            WebhookConstants.OutgoingHttpClientName
        );
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

    protected override Task HandleErrorAsync(
        OutgoingWebhookItem item,
        Exception ex,
        CancellationToken ct
    )
    {
        _logger.LogError(ex, "Failed to deliver outgoing webhook to {Url}", item.CallbackUrl);
        return Task.CompletedTask;
    }

    private static async Task ValidateCallbackUrlAsync(string callbackUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out Uri? uri))
            throw new InvalidOperationException(
                $"Callback URL '{callbackUrl}' is not a valid absolute URI."
            );

        if (!AllowedSchemes.Contains(uri.Scheme))
            throw new InvalidOperationException(
                $"Callback URL scheme '{uri.Scheme}' is not allowed. Only HTTP and HTTPS are permitted."
            );

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct);

        foreach (IPAddress address in addresses)
        {
            if (IsProhibitedAddress(address))
                throw new InvalidOperationException(
                    $"Callback URL '{uri.Host}' resolves to a prohibited address ({address}). "
                        + "Requests to loopback, private, and link-local networks are not allowed."
                );
        }
    }

    private static bool IsProhibitedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv6LinkLocal)
            return true;

        byte[] bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] switch
            {
                10 => true, // 10.0.0.0/8
                172 => bytes[1] >= 16 && bytes[1] <= 31, // 172.16.0.0/12
                192 => bytes[1] == 168, // 192.168.0.0/16
                169 => bytes[1] == 254, // 169.254.0.0/16 (link-local)
                _ => false,
            };
        }

        return false;
    }
}
