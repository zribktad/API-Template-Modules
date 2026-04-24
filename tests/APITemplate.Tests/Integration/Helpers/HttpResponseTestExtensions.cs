using System.Net;
using System.Text.Json;
using Shouldly;

namespace APITemplate.Tests.Integration.Helpers;

internal static class HttpResponseTestExtensions
{
    internal static async Task<T> ReadJsonAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default
    )
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        try
        {
            return JsonSerializer.Deserialize<T>(body, TestJsonOptions.CaseInsensitive)
                ?? throw new InvalidOperationException($"Response body is null. Raw: {body}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Response body could not be deserialized to {typeof(T).Name}. Raw: {body}",
                ex
            );
        }
    }

    internal static async Task<HttpResponseMessage> ShouldBeStatusAsync(
        this HttpResponseMessage response,
        HttpStatusCode expected,
        CancellationToken ct = default
    )
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            response.StatusCode.ShouldBe(expected, body);
        }
        return response;
    }

    internal static Task<HttpResponseMessage> ShouldBeOkAsync(
        this HttpResponseMessage response,
        CancellationToken ct = default
    ) => response.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
}
