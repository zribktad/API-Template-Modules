using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Common.Events;
using SharedKernel.Application.Events;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

[Collection(IntegrationCollectionNames.HttpRead)]
public sealed class WolverineNotificationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public WolverineNotificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CacheInvalidationNotification_CanBePublished()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var exception = await Record.ExceptionAsync(() =>
            bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Products)).AsTask()
        );

        exception.ShouldBeNull();
    }
}
