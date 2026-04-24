using FileStorage.Domain.Storage;
using FileStorage.Services;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

[Trait("Category", "Unit")]
public sealed class BlobStoreFactoryTests
{
    [Fact]
    public void Get_KnownKey_ReturnsRegisteredStore()
    {
        Mock<IBlobStore> local = new();
        BlobStoreFactory sut = new([new KeyedBlobStore("local", local.Object)]);

        sut.Get("local").ShouldBeSameAs(local.Object);
    }

    [Fact]
    public void Get_UnknownKey_Throws()
    {
        BlobStoreFactory sut = new([new KeyedBlobStore("local", Mock.Of<IBlobStore>())]);
        Should.Throw<InvalidOperationException>(() => sut.Get("s3"));
    }

    [Fact]
    public void Get_EmptyKey_Throws()
    {
        BlobStoreFactory sut = new([new KeyedBlobStore("local", Mock.Of<IBlobStore>())]);
        Should.Throw<ArgumentException>(() => sut.Get(""));
    }

    [Theory]
    [InlineData("Local", "LOCAL")]
    [InlineData("local", "Local")]
    [InlineData("LOCAL", "local")]
    public void Get_KeyLookupIsCaseInsensitive(string registered, string lookup)
    {
        Mock<IBlobStore> local = new();
        BlobStoreFactory sut = new([new KeyedBlobStore(registered, local.Object)]);
        sut.Get(lookup).ShouldBeSameAs(local.Object);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Get_WhitespaceKey_Throws(string key)
    {
        BlobStoreFactory sut = new([new KeyedBlobStore("local", Mock.Of<IBlobStore>())]);
        Should.Throw<ArgumentException>(() => sut.Get(key));
    }

    [Fact]
    public void Get_MultipleBackends_ResolvesEachDistinctly()
    {
        Mock<IBlobStore> local = new();
        Mock<IBlobStore> s3 = new();
        Mock<IBlobStore> azure = new();
        BlobStoreFactory sut = new([
            new KeyedBlobStore("local", local.Object),
            new KeyedBlobStore("s3", s3.Object),
            new KeyedBlobStore("azure", azure.Object),
        ]);

        sut.Get("local").ShouldBeSameAs(local.Object);
        sut.Get("s3").ShouldBeSameAs(s3.Object);
        sut.Get("azure").ShouldBeSameAs(azure.Object);
    }
}
