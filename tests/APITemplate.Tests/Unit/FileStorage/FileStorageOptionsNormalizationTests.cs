using FileStorage.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

/// <summary>
///     Verifies that the module's PostConfigure hook lowercases <see cref="FileStorageOptions.AllowedExtensions" />
///     so the upload handler can rely on a normalized whitelist regardless of config casing.
/// </summary>
public sealed class FileStorageOptionsNormalizationTests
{
    [Fact]
    public void PostConfigure_LowercasesAllowedExtensions()
    {
        ServiceCollection services = new();

        services
            .AddOptions<FileStorageOptions>()
            .Configure(o =>
            {
                o.AllowedExtensions = [".PNG", ".JpG", ".pdf"];
            });

        services.PostConfigure<FileStorageOptions>(opts =>
        {
            opts.AllowedExtensions = opts
                .AllowedExtensions.Select(e => e.ToLowerInvariant())
                .ToArray();
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        FileStorageOptions resolved = provider
            .GetRequiredService<IOptions<FileStorageOptions>>()
            .Value;

        resolved.AllowedExtensions.ShouldBe([".png", ".jpg", ".pdf"]);
    }
}
