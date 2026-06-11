using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Commands.Webhooks;
using Webhooks.Contracts;
using Webhooks.Logging;

namespace Webhooks.Features.SendWebhookCallback;

/// <summary>
///     Wolverine handler that delivers an outgoing webhook callback over HTTP.
///     The delivery runs inside the durable message handler itself — there is no in-memory channel hop —
///     so Wolverine's durable inbox/outbox, retry policy, and dead-letter table govern at-least-once delivery.
///     Transient failures (non-2xx responses, timeouts) are rethrown to trigger Wolverine retries.
/// </summary>
public sealed class SendWebhookCallbackHandler
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https",
        "http",
    };

    public static async Task HandleAsync(
        SendWebhookCallbackCommand command,
        IHttpClientFactory httpClientFactory,
        IWebhookPayloadSigner signer,
        IHostEnvironment environment,
        IOptions<WebhookOptions> options,
        ILogger<SendWebhookCallbackHandler> logger,
        CancellationToken ct
    )
    {
        bool allowPlainHttp = environment.IsDevelopment() && options.Value.AllowLocalRequests;
        ValidateCallbackUrl(command.CallbackUrl, allowPlainHttp);

        // Query strings can carry capability tokens — log only scheme/host/path, never the query.
        string safeUrl = new Uri(command.CallbackUrl).GetLeftPart(UriPartial.Path);

        WebhookSignatureResult signatureResult = signer.Sign(command.SerializedPayload);

        using HttpClient client = httpClientFactory.CreateClient(
            WebhookConstants.OutgoingHttpClientName
        );
        using HttpRequestMessage request = new(HttpMethod.Post, command.CallbackUrl)
        {
            Content = new StringContent(
                command.SerializedPayload,
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")
            ),
        };

        request.Headers.Add(WebhookConstants.SignatureHeader, signatureResult.Signature);
        request.Headers.Add(WebhookConstants.TimestampHeader, signatureResult.Timestamp);
        request.Headers.Add(WebhookConstants.EventIdHeader, command.EventId);

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed (not a host shutdown) — surface as a transient timeout so
            // Wolverine's durable retry policy reschedules the delivery instead of dead-lettering it.
            logger.OutgoingWebhookDeliveryFailed(
                new TimeoutException($"Webhook delivery to '{safeUrl}' timed out."),
                safeUrl
            );
            throw new TimeoutException($"Webhook delivery to '{safeUrl}' timed out.");
        }

        logger.OutgoingWebhookDelivered(safeUrl);
    }

    private static void ValidateCallbackUrl(string callbackUrl, bool allowPlainHttp)
    {
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException(
                $"Callback URL '{callbackUrl}' is not a valid absolute URI."
            );
        }

        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            throw new InvalidOperationException(
                $"Callback URL scheme '{uri.Scheme}' is not allowed. Only HTTP and HTTPS are permitted."
            );
        }

        // Enforce HTTPS outside Development (plain HTTP only when local requests are explicitly enabled).
        if (
            !allowPlainHttp
            && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                $"Callback URL scheme '{uri.Scheme}' is not allowed; webhook callbacks must use HTTPS."
            );
        }
    }
}
