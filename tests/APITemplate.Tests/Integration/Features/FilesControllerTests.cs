using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class FilesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FilesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_ValidFile_Returns201WithMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var content = CreateMultipartContent("test.txt", "text/plain", "Hello world");
        var response = await _client.PostAsync("/api/v1/files/upload", content, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);

        var result = JsonSerializer.Deserialize<FileUploadResponse>(
            body,
            TestJsonOptions.CaseInsensitive
        );
        result.ShouldNotBeNull();
        result!.OriginalFileName.ShouldBe("test.txt");
        result.ContentType.ShouldBe("text/plain");
        result.SizeBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Download_AfterUpload_ReturnsMatchingContent()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var fileContent = "Download test content";
        var uploadContent = CreateMultipartContent("download-test.txt", "text/plain", fileContent);
        var uploadResponse = await _client.PostAsync("/api/v1/files/upload", uploadContent, ct);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync(ct);
        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.Created, uploadBody);

        var uploaded = JsonSerializer.Deserialize<FileUploadResponse>(
            uploadBody,
            TestJsonOptions.CaseInsensitive
        )!;

        var downloadResponse = await _client.GetAsync($"/api/v1/files/{uploaded.Id}/download", ct);
        downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var downloadedContent = await downloadResponse.Content.ReadAsStringAsync(ct);
        downloadedContent.ShouldBe(fileContent);
    }

    [Fact]
    public async Task Upload_InvalidExtension_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var content = CreateMultipartContent(
            "malware.exe",
            "application/octet-stream",
            "bad stuff"
        );
        var response = await _client.PostAsync("/api/v1/files/upload", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(
            body,
            TestJsonOptions.CaseInsensitive
        );
        problem.ShouldNotBeNull();
        problem!.Detail.ShouldBe("File type '.exe' is not allowed.");
        problem.Extensions.ShouldContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().ShouldBe("EXA-0400-FILE");
    }

    [Fact]
    public async Task Download_NonExistentId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync($"/api/v1/files/{Guid.NewGuid()}/download", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static MultipartFormDataContent CreateMultipartContent(
        string fileName,
        string contentType,
        string content
    )
    {
        var fileBytes = Encoding.UTF8.GetBytes(content);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var formData = new MultipartFormDataContent();
        formData.Add(fileContent, "file", fileName);
        return formData;
    }
}
