using System.Net;
using System.Net.Http.Headers;
using System.Text;
using APITemplate.Tests.Integration.Helpers;
using BuildingBlocks.Security;
using FileStorage.Contracts;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class FileStorageApiIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _tenant = default!;
    private AppUser _user = default!;

    public FileStorageApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_tenant, _user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "filestorage_admin",
            "filestorage_admin@test.com",
            ct: ct
        );
        _client.AuthenticateAs(
            _tenant,
            _user,
            Permission.Examples.Upload,
            Permission.Examples.Download
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(0L, 4L, "ABCDE")] // 0-4 inclusive
    [InlineData(5L, null, "FGHIJ")] // 5 to end
    [InlineData(null, 3L, "HIJ")] // Last 3 bytes
    public async Task Download_WithRangeHeader_ReturnsPartialContent(
        long? from,
        long? to,
        string expected
    )
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // 1. Upload
        byte[] content = "ABCDEFGHIJ"u8.ToArray(); // 10 bytes
        FileUploadResponse uploadedFile = await UploadTestFileAsync(content, "test.txt", ct);

        // 2. Request
        HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        request.Headers.Range = new RangeHeaderValue(from, to);

        HttpResponseMessage response = await _client.SendAsync(request, ct);

        // 3. Assert
        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange.ShouldNotBeNull();

        long expectedFrom,
            expectedTo;
        if (from.HasValue && to.HasValue)
        {
            expectedFrom = from.Value;
            expectedTo = to.Value;
        }
        else if (from.HasValue)
        {
            expectedFrom = from.Value;
            expectedTo = content.Length - 1;
        }
        else // suffix range
        {
            expectedFrom = content.Length - to!.Value;
            expectedTo = content.Length - 1;
        }

        response.Content.Headers.ContentRange!.From.ShouldBe(expectedFrom);
        response.Content.Headers.ContentRange!.To.ShouldBe(expectedTo);
        response.Content.Headers.ContentRange!.Length.ShouldBe(content.Length);

        byte[] returnedBytes = await response.Content.ReadAsByteArrayAsync(ct);
        returnedBytes.ShouldBe(Encoding.UTF8.GetBytes(expected));
    }

    [Fact]
    public async Task Download_WithETag_ReturnsNotModified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        byte[] content = "Conditional request test"u8.ToArray();
        FileUploadResponse uploadedFile = await UploadTestFileAsync(content, "conditional.txt", ct);

        HttpResponseMessage initialResponse = await _client.GetAsync(
            $"/api/v1/files/{uploadedFile.Id}/download",
            ct
        );
        await initialResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        EntityTagHeaderValue? etag = initialResponse.Headers.ETag;
        etag.ShouldNotBeNull();

        HttpRequestMessage conditionalRequest = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        conditionalRequest.Headers.IfNoneMatch.Add(etag);

        HttpResponseMessage conditionalResponse = await _client.SendAsync(conditionalRequest, ct);
        conditionalResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Download_FullFile_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        byte[] content = "Full file download test content"u8.ToArray();
        FileUploadResponse uploadedFile = await UploadTestFileAsync(content, "full.txt", ct);

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/files/{uploadedFile.Id}/download",
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().ShouldBe("text/plain");
        byte[] downloadedBytes = await response.Content.ReadAsByteArrayAsync(ct);
        downloadedBytes.ShouldBe(content);
    }

    [Fact]
    public async Task Download_WithInvalidRange_ReturnsRequestedRangeNotSatisfiable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        byte[] content = "Short"u8.ToArray();
        FileUploadResponse uploadedFile = await UploadTestFileAsync(content, "short.txt", ct);

        HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        request.Headers.Range = new RangeHeaderValue(10, 20);

        HttpResponseMessage response = await _client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.RequestedRangeNotSatisfiable);
    }

    [Fact]
    public async Task Download_WithIfRange_ReturnsPartialContentIfMatches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        byte[] content = "If-Range test content"u8.ToArray();
        FileUploadResponse uploadedFile = await UploadTestFileAsync(content, "ifrange.txt", ct);

        HttpResponseMessage initial = await _client.GetAsync(
            $"/api/v1/files/{uploadedFile.Id}/download",
            ct
        );
        EntityTagHeaderValue etag = initial.Headers.ETag!;

        HttpRequestMessage requestMatch = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        requestMatch.Headers.Range = new RangeHeaderValue(0, 4);
        requestMatch.Headers.IfRange = new RangeConditionHeaderValue(etag);

        HttpResponseMessage responseMatch = await _client.SendAsync(requestMatch, ct);
        responseMatch.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await responseMatch.Content.ReadAsByteArrayAsync(ct)).ShouldBe("If-Ra"u8.ToArray());

        HttpRequestMessage requestMismatch = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        requestMismatch.Headers.Range = new RangeHeaderValue(0, 4);
        requestMismatch.Headers.IfRange = new RangeConditionHeaderValue("\"mismatch\"");

        HttpResponseMessage responseMismatch = await _client.SendAsync(requestMismatch, ct);
        responseMismatch.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await responseMismatch.Content.ReadAsByteArrayAsync(ct)).ShouldBe(content);
    }

    [Fact]
    public async Task Download_Resumable_CanRecoverAfterInterruption()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // 1. Upload
        byte[] content = Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ");
        FileUploadResponse uploadedFile = await UploadTestFileAsync(content, "resumable.txt", ct);

        // 2. Initial partial request (simulating first chunk before "crash")
        HttpRequestMessage firstRequest = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        firstRequest.Headers.Range = new RangeHeaderValue(0, 9);
        HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, ct);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        byte[] firstPart = await firstResponse.Content.ReadAsByteArrayAsync(ct);
        firstPart.Length.ShouldBe(10);
        Encoding.UTF8.GetString(firstPart).ShouldBe("0123456789");

        // Get ETag to ensure we resume the same version
        EntityTagHeaderValue? etag = firstResponse.Headers.ETag;

        // 3. Resume request (simulating recovery using Range header)
        HttpRequestMessage resumeRequest = new(
            HttpMethod.Get,
            $"/api/v1/files/{uploadedFile.Id}/download"
        );
        resumeRequest.Headers.Range = new RangeHeaderValue(10, null); // bytes=10-
        if (etag != null)
        {
            resumeRequest.Headers.IfRange = new RangeConditionHeaderValue(etag);
        }

        HttpResponseMessage resumeResponse = await _client.SendAsync(resumeRequest, ct);

        resumeResponse.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        byte[] secondPart = await resumeResponse.Content.ReadAsByteArrayAsync(ct);
        secondPart.Length.ShouldBe(10);
        Encoding.UTF8.GetString(secondPart).ShouldBe("ABCDEFGHIJ");

        // 4. Final integrity check - verify the whole file
        byte[] combined = new byte[firstPart.Length + secondPart.Length];
        Buffer.BlockCopy(firstPart, 0, combined, 0, firstPart.Length);
        Buffer.BlockCopy(secondPart, 0, combined, firstPart.Length, secondPart.Length);

        combined.ShouldBe(content);
        Encoding.UTF8.GetString(combined).ShouldBe("0123456789ABCDEFGHIJ");
    }

    private async Task<FileUploadResponse> UploadTestFileAsync(
        byte[] content,
        string fileName,
        CancellationToken ct = default
    )
    {
        using MultipartFormDataContent formData = new();
        ByteArrayContent filePart = new(content);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formData.Add(filePart, "File", fileName);
        formData.Add(new StringContent("Test file"), "Description");

        HttpResponseMessage uploadResponse = await _client.PostAsync(
            "/api/v1/files/upload",
            formData,
            ct
        );
        await uploadResponse.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        return (await uploadResponse.ReadJsonAsync<FileUploadResponse>(ct))!;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Download_Resumable_FallbackToFullDownloadWhenETagMismatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // 1. Upload
        byte[] content = Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ");
        FileUploadResponse upload = await UploadTestFileAsync(
            content,
            "resumable_fallback.txt",
            ct
        );

        // 2. Request with WRONG ETag in If-Range
        // RFC 7233: If the validator does not match, the server MUST ignore the Range header
        // and return the full resource (200 OK).
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/v1/files/{upload.Id}/download"
        );
        request.Headers.Range = new RangeHeaderValue(10, null); // bytes=10-
        request.Headers.IfRange = new RangeConditionHeaderValue("\"wrong-etag\"");

        using HttpResponseMessage response = await _client.SendAsync(request, ct);

        // 3. Verify fallback
        response.StatusCode.ShouldBe(HttpStatusCode.OK); // Not 206!
        byte[] received = await response.Content.ReadAsByteArrayAsync(ct);
        received.Length.ShouldBe(content.Length);
        received.ShouldBe(content);
    }
}
