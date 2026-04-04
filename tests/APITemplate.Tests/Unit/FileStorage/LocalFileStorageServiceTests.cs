using FileStorage.Contracts;
using FileStorage.Services;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel.Application.Context;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _basePath = Path.Combine(
        Path.GetTempPath(),
        "fs-u-" + Guid.NewGuid().ToString("N")
    );
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, recursive: true);
        }
        catch
        {
            // best-effort cleanup for parallel CI
        }
    }

    [Fact]
    public async Task SaveAsync_WritesFileUnderTenantDirectory()
    {
        Guid tenantId = Guid.NewGuid();
        Mock<ITenantProvider> tenant = new();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        LocalFileStorageService sut = CreateSut(tenant.Object);
        await using MemoryStream input = new("hello"u8.ToArray());

        FileStorageResult result = await sut.SaveAsync(
            input,
            "doc.pdf",
            TestContext.Current.CancellationToken
        );

        result.SizeBytes.ShouldBe(5);
        File.Exists(result.StoragePath).ShouldBeTrue();
        result.StoragePath.ShouldStartWith(Path.Combine(_basePath, tenantId.ToString()));
    }

    [Fact]
    public async Task OpenReadAsync_WhenMissing_ReturnsNull()
    {
        LocalFileStorageService sut = CreateSut(CreateTenant());

        Stream? stream = await sut.OpenReadAsync(
            Path.Combine(_basePath, Guid.NewGuid().ToString(), "missing.bin"),
            TestContext.Current.CancellationToken
        );

        stream.ShouldBeNull();
    }

    [Fact]
    public async Task OpenReadAsync_WhenOutsideBasePath_Throws()
    {
        LocalFileStorageService sut = CreateSut(CreateTenant());

        string outside = Path.GetFullPath(Path.Combine(_basePath, "..", "outside-test.txt"));

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            sut.OpenReadAsync(outside, TestContext.Current.CancellationToken)
        );
    }

    private static ITenantProvider CreateTenant()
    {
        Mock<ITenantProvider> tenant = new();
        tenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        return tenant.Object;
    }

    private LocalFileStorageService CreateSut(ITenantProvider tenant)
    {
        Directory.CreateDirectory(_basePath);
        IOptions<FileStorageOptions> options = Options.Create(
            new FileStorageOptions { BasePath = _basePath }
        );
        return new LocalFileStorageService(options, tenant);
    }
}
