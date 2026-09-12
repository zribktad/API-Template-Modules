using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using APITemplate.ApiClient;
using APITemplate.ApiClient.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Clients;

[Trait("Category", "Unit")]
public sealed class ApiClientEnumSerializationTests
{
    public ApiClientEnumSerializationTests()
    {
        // Ensure Kiota JSON serialization/deserialization handlers are registered
        ParseNodeFactoryRegistry.DefaultInstance.ContentTypeAssociatedFactories[
            "application/json"
        ] = new JsonParseNodeFactory();
        SerializationWriterFactoryRegistry.DefaultInstance.ContentTypeAssociatedFactories[
            "application/json"
        ] = new JsonSerializationWriterFactory();
    }

    [Fact]
    public async Task InvitationStatus_SerializesToString_AndDeserializesBack()
    {
        // Arrange
        var invitation = new TenantInvitationResponse
        {
            Id = Guid.NewGuid(),
            Email = "invitee@example.com",
            Status = InvitationStatus.Accepted,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
        };

        var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(null, invitation);

        using var stream = writer.GetSerializedContent();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string json = reader.ReadToEnd();

        // Assert serialization outputs string enum, not int
        json.ShouldContain("\"status\":\"Accepted\"");
        json.ShouldNotContain("\"status\":1");

        // Act - Deserialization
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using var readStream = new MemoryStream(bytes);
        IParseNode parseNode = await new JsonParseNodeFactory().GetRootParseNodeAsync(
            "application/json",
            readStream,
            TestContext.Current.CancellationToken
        );
        var result = parseNode.GetObjectValue(
            TenantInvitationResponse.CreateFromDiscriminatorValue
        );

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(InvitationStatus.Accepted);
        result.Email.ShouldBe("invitee@example.com");
    }

    [Fact]
    public async Task JobStatus_SerializesToString_AndDeserializesBack()
    {
        // Arrange
        var job = new JobStatusResponse
        {
            Id = Guid.NewGuid(),
            JobType = "EmailDigest",
            Status = JobStatus.Processing,
            ProgressPercent = 50,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
        };

        var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(null, job);

        using var stream = writer.GetSerializedContent();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string json = reader.ReadToEnd();

        // Assert serialization outputs string enum
        json.ShouldContain("\"status\":\"Processing\"");
        json.ShouldNotContain("\"status\":1");

        // Act - Deserialization
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using var readStream = new MemoryStream(bytes);
        IParseNode parseNode = await new JsonParseNodeFactory().GetRootParseNodeAsync(
            "application/json",
            readStream,
            TestContext.Current.CancellationToken
        );
        var result = parseNode.GetObjectValue(JobStatusResponse.CreateFromDiscriminatorValue);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(JobStatus.Processing);
        result.JobType.ShouldBe("EmailDigest");
        result.ProgressPercent.ShouldBe(50);
    }

    [Fact]
    public async Task ApiClient_CallsTenantInvitations_ReturnsTypedResponse()
    {
        // Arrange
        var expectedInvitation = new TenantInvitationResponse
        {
            Id = Guid.NewGuid(),
            Email = "test@company.com",
            Status = InvitationStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
        };

        var mockAdapter = new Mock<IRequestAdapter>();
        mockAdapter
            .SetupGet(a => a.SerializationWriterFactory)
            .Returns(SerializationWriterFactoryRegistry.DefaultInstance);
        mockAdapter
            .Setup(a =>
                a.SendAsync(
                    It.IsAny<RequestInformation>(),
                    It.IsAny<ParsableFactory<TenantInvitationResponse>>(),
                    It.IsAny<Dictionary<string, ParsableFactory<IParsable>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedInvitation);

        mockAdapter.SetupProperty(a => a.BaseUrl, "https://api.example.com");

        global::APITemplate.ApiClient.ApiClient client = new(mockAdapter.Object);

        // Act
        var request = new CreateTenantInvitationRequest { Email = "test@company.com" };
        var result = await client.Api.V1.TenantInvitations.PostAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(InvitationStatus.Pending);
        result.Email.ShouldBe("test@company.com");
    }
}
