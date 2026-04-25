using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
[Trait("Category", "Unit.Component")]
public sealed class ScopedBffSessionDbContextFactoryTests
{
    [Fact]
    public void Create_DisposesScope_WhenDbContextResolutionFails()
    {
        TrackingScopeFactory scopeFactory = new();
        ScopedBffSessionDbContextFactory sut = new(scopeFactory);

        Should.Throw<InvalidOperationException>(() => sut.Create());

        scopeFactory.Scope.DisposeCount.ShouldBe(1);
    }

    private sealed class TrackingScopeFactory : IServiceScopeFactory
    {
        public TrackingServiceScope Scope { get; } = new();

        public IServiceScope CreateScope()
        {
            return Scope;
        }
    }

    private sealed class TrackingServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
