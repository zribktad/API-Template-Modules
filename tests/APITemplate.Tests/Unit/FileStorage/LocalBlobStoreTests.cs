using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain.Services;
using FileStorage.Domain.Storage;
using FileStorage.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using Polly.Retry;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Resilience;
using Shouldly;
using Xunit;
using FS = FileStorage.Domain.ErrorCatalog;

namespace APITemplate.Tests.Unit.FileStorage;

[Trait("Category", "Unit")]
public sealed class LocalBlobStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "api-template-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly FileStorageOptions _options;

    public LocalBlobStoreTests()
    {
        _options = new FileStorageOptions
        {
            BasePath = _tempDir,
            MaxFileSizeBytes = 10 * 1024 * 1024,
            AllowedExtensions = [".txt"],
            StagingTtlMinutes = 30,
            BackendKey = "local",
        };
    }

    private LocalBlobStore CreateSut()
    {
        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder(
            ResiliencePipelineKeys.FileStorageDelete,
            (b, _) => b.AddRetry(new RetryStrategyOptions { Delay = TimeSpan.Zero })
        );
        FileStorageDeletePipelineProvider deleteProvider = new(registry);
        return new LocalBlobStore(
            Options.Create(_options),
            deleteProvider,
            NullLogger<LocalBlobStore>.Instance
        );
    }

    private static MemoryStream Payload(string s) => new(Encoding.UTF8.GetBytes(s));

    private static string ExpectedSha(string s) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    [Fact]
    public async Task WriteStagingAsync_ComputesSha256AndPersistsToStaging()
    {
        LocalBlobStore sut = CreateSut();
        ErrorOr<StagingResult> result = await sut.WriteStagingAsync(
            Payload("hello world"),
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        StagingResult staging = result.Value;
        staging.Sha256.ShouldBe(ExpectedSha("hello world"));
        staging.SizeBytes.ShouldBe(11);
        File.Exists(staging.StagingPath).ShouldBeTrue();
        staging.StagingPath.ShouldContain("staging");
    }

    [Fact]
    public async Task PromoteToCommittedAsync_AtomicallyMovesToContentAddressedPath()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenant = Guid.NewGuid();
        StagingResult staging = (
            await sut.WriteStagingAsync(Payload("atomic"), TestContext.Current.CancellationToken)
        ).Value;

        ErrorOr<string> result = await sut.PromoteToCommittedAsync(
            tenant,
            staging.Sha256,
            staging.SizeBytes,
            staging.StagingPath,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        string committed = result.Value;
        File.Exists(committed).ShouldBeTrue();
        File.Exists(staging.StagingPath).ShouldBeFalse();
        committed.ShouldContain(tenant.ToString());
        committed.ShouldEndWith(staging.Sha256);
    }

    [Fact]
    public async Task PromoteToCommittedAsync_IsIdempotentForIdenticalContent()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenant = Guid.NewGuid();

        StagingResult first = (
            await sut.WriteStagingAsync(Payload("dup"), TestContext.Current.CancellationToken)
        ).Value;
        string committed1 = (
            await sut.PromoteToCommittedAsync(
                tenant,
                first.Sha256,
                first.SizeBytes,
                first.StagingPath,
                TestContext.Current.CancellationToken
            )
        ).Value;

        StagingResult second = (
            await sut.WriteStagingAsync(Payload("dup"), TestContext.Current.CancellationToken)
        ).Value;
        string committed2 = (
            await sut.PromoteToCommittedAsync(
                tenant,
                second.Sha256,
                second.SizeBytes,
                second.StagingPath,
                TestContext.Current.CancellationToken
            )
        ).Value;

        committed1.ShouldBe(committed2);
        File.Exists(second.StagingPath).ShouldBeFalse();
    }

    [Fact]
    public async Task PromoteToCommittedAsync_ReturnsConflictErrorOnSizeMismatch()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenant = Guid.NewGuid();
        StagingResult first = (
            await sut.WriteStagingAsync(Payload("legit"), TestContext.Current.CancellationToken)
        ).Value;
        await sut.PromoteToCommittedAsync(
            tenant,
            first.Sha256,
            first.SizeBytes,
            first.StagingPath,
            TestContext.Current.CancellationToken
        );

        StagingResult fake = (
            await sut.WriteStagingAsync(Payload("x"), TestContext.Current.CancellationToken)
        ).Value;

        ErrorOr<string> result = await sut.PromoteToCommittedAsync(
            tenant,
            first.Sha256,
            first.SizeBytes + 999,
            fake.StagingPath,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(FS.Files.BlobConflict);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsNotFoundErrorWhenBlobMissing()
    {
        LocalBlobStore sut = CreateSut();
        ErrorOr<Stream> result = await sut.OpenReadAsync(
            Guid.NewGuid(),
            ExpectedSha("missing"),
            TestContext.Current.CancellationToken
        );
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(FS.Files.FileNotFound);
    }

    [Fact]
    public async Task OpenReadAsync_ReadsCommittedBlob()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenant = Guid.NewGuid();
        StagingResult staging = (
            await sut.WriteStagingAsync(Payload("readback"), TestContext.Current.CancellationToken)
        ).Value;
        await sut.PromoteToCommittedAsync(
            tenant,
            staging.Sha256,
            staging.SizeBytes,
            staging.StagingPath,
            TestContext.Current.CancellationToken
        );

        ErrorOr<Stream> openResult = await sut.OpenReadAsync(
            tenant,
            staging.Sha256,
            TestContext.Current.CancellationToken
        );
        openResult.IsError.ShouldBeFalse();
        await using Stream stream = openResult.Value;
        using StreamReader reader = new(stream);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("readback");
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotentAndSwallowsMissingFiles()
    {
        LocalBlobStore sut = CreateSut();
        await Should.NotThrowAsync(() => sut.DeleteAsync(Guid.NewGuid(), ExpectedSha("nope")));
    }

    [Fact]
    public async Task DeleteStagingAsync_IsIdempotent()
    {
        LocalBlobStore sut = CreateSut();
        string fake = Path.Combine(_options.ResolveStagingPath(), "no-such-file");
        Directory.CreateDirectory(_options.ResolveStagingPath());
        await Should.NotThrowAsync(() => sut.DeleteStagingAsync(fake));
    }

    [Fact]
    public async Task PromoteToCommittedAsync_ReturnsPathTraversalErrorForEvilPath()
    {
        LocalBlobStore sut = CreateSut();
        string evil = Path.Combine(_options.ResolveStagingPath(), "..", "..", "escape");
        ErrorOr<string> result = await sut.PromoteToCommittedAsync(
            Guid.NewGuid(),
            ExpectedSha("x"),
            1,
            evil,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(FS.Files.PathTraversal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(4096)]
    [InlineData(81920)] // exactly one buffer
    [InlineData(81921)] // one-byte overflow
    [InlineData(200_000)] // multi-buffer
    public async Task WriteStagingAsync_ComputesCorrectHashForVariousSizes(int size)
    {
        byte[] payload = new byte[size];
        new Random(size).NextBytes(payload);
        LocalBlobStore sut = CreateSut();

        ErrorOr<StagingResult> result = await sut.WriteStagingAsync(
            new MemoryStream(payload),
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        StagingResult staging = result.Value;
        staging.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(payload)));
        staging.SizeBytes.ShouldBe(size);
        new FileInfo(staging.StagingPath).Length.ShouldBe(size);
    }

    [Fact]
    public async Task WriteStagingAsync_ExceedsMaxSize_ReturnsErrorAndCleansUp()
    {
        _options.MaxFileSizeBytes = 1024;
        byte[] payload = new byte[2048];
        LocalBlobStore sut = CreateSut();

        ErrorOr<StagingResult> result = await sut.WriteStagingAsync(
            new MemoryStream(payload),
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        string stagingDir = _options.ResolveStagingPath();
        if (Directory.Exists(stagingDir))
            Directory.EnumerateFiles(stagingDir).ShouldBeEmpty();
    }

    [Fact]
    public async Task PromoteToCommittedAsync_PathLayoutIsShaPrefixed()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenant = Guid.NewGuid();
        StagingResult staging = (
            await sut.WriteStagingAsync(
                Payload("prefix-check"),
                TestContext.Current.CancellationToken
            )
        ).Value;
        string committed = (
            await sut.PromoteToCommittedAsync(
                tenant,
                staging.Sha256,
                staging.SizeBytes,
                staging.StagingPath,
                TestContext.Current.CancellationToken
            )
        ).Value;

        string expectedPrefixDir = Path.Combine(
            _options.ResolveBlobsPath(),
            tenant.ToString(),
            staging.Sha256[..2]
        );
        committed.ShouldStartWith(expectedPrefixDir);
    }

    [Fact]
    public async Task PromoteToCommittedAsync_DifferentTenantsDoNotShareBlobs()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        StagingResult stagingA = (
            await sut.WriteStagingAsync(
                Payload("same-content"),
                TestContext.Current.CancellationToken
            )
        ).Value;
        string committedA = (
            await sut.PromoteToCommittedAsync(
                tenantA,
                stagingA.Sha256,
                stagingA.SizeBytes,
                stagingA.StagingPath,
                TestContext.Current.CancellationToken
            )
        ).Value;

        StagingResult stagingB = (
            await sut.WriteStagingAsync(
                Payload("same-content"),
                TestContext.Current.CancellationToken
            )
        ).Value;
        string committedB = (
            await sut.PromoteToCommittedAsync(
                tenantB,
                stagingB.Sha256,
                stagingB.SizeBytes,
                stagingB.StagingPath,
                TestContext.Current.CancellationToken
            )
        ).Value;

        committedA.ShouldNotBe(committedB);
        File.Exists(committedA).ShouldBeTrue();
        File.Exists(committedB).ShouldBeTrue();
        committedA.ShouldContain(tenantA.ToString());
        committedB.ShouldContain(tenantB.ToString());
    }

    [Fact]
    public async Task DeleteAsync_RemovesCommittedBlob()
    {
        LocalBlobStore sut = CreateSut();
        Guid tenant = Guid.NewGuid();
        StagingResult staging = (
            await sut.WriteStagingAsync(Payload("bye"), TestContext.Current.CancellationToken)
        ).Value;
        string committed = (
            await sut.PromoteToCommittedAsync(
                tenant,
                staging.Sha256,
                staging.SizeBytes,
                staging.StagingPath,
                TestContext.Current.CancellationToken
            )
        ).Value;
        File.Exists(committed).ShouldBeTrue();

        await sut.DeleteAsync(tenant, staging.Sha256, TestContext.Current.CancellationToken);

        File.Exists(committed).ShouldBeFalse();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("../../etc/passwd")]
    public async Task DeleteStagingAsync_RejectsTraversalPath(string suffix)
    {
        LocalBlobStore sut = CreateSut();
        string evil = Path.Combine(_options.ResolveStagingPath(), suffix);
        AppException exception = await Should.ThrowAsync<AppException>(async () =>
            await sut.DeleteStagingAsync(evil)
        );
        exception.ErrorCode.ShouldBe(FS.Files.PathTraversal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
