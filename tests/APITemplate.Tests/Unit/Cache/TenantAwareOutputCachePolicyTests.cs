using APITemplate.Api.Cache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Cache;

public sealed class TenantAwareOutputCachePolicyTests
{
    [Fact]
    public async Task CacheRequestAsync_EnablesCacheLookupAndStorage()
    {
        var sut = new TenantAwareOutputCachePolicy();
        var context = CreateContext();

        await sut.CacheRequestAsync(context, TestContext.Current.CancellationToken);

        context.EnableOutputCaching.ShouldBeTrue();
        context.AllowCacheLookup.ShouldBeTrue();
        context.AllowCacheStorage.ShouldBeTrue();
    }

    [Fact]
    public async Task ServeFromCacheAsync_CompletesWithoutError()
    {
        var sut = new TenantAwareOutputCachePolicy();
        var context = CreateContext();

        await sut.ServeFromCacheAsync(context, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task CacheRequestAsync_NonGetHeadMethod_DoesNotEnableCache(string method)
    {
        var sut = new TenantAwareOutputCachePolicy();
        var context = CreateContext(method);

        await sut.CacheRequestAsync(context, TestContext.Current.CancellationToken);

        context.EnableOutputCaching.ShouldBeFalse();
        context.AllowCacheLookup.ShouldBeFalse();
        context.AllowCacheStorage.ShouldBeFalse();
    }

    [Fact]
    public async Task ServeResponseAsync_WhenStorageDisabled_CompletesWithoutError()
    {
        var sut = new TenantAwareOutputCachePolicy();
        var context = CreateContext();
        context.AllowCacheStorage = false;

        await sut.ServeResponseAsync(context, TestContext.Current.CancellationToken);
    }

    private static OutputCacheContext CreateContext(string method = "GET")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = "/api/v1/products";
        return new OutputCacheContext { HttpContext = httpContext };
    }
}
