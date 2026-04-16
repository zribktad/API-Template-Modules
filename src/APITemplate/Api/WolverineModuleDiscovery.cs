using System.Collections.Immutable;
using System.Reflection;
using BackgroundJobs.Features;
using Chatting.Features.GetNotificationStream;
using FileStorage.Features.Upload;
using Identity.Directory.Features.User;
using Identity.Directory.Handlers;
using Notifications.Features;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.Handlers;
using Reviews.Features;
using Webhooks.Features.SendWebhookCallback;

namespace APITemplate.Api;

/// <summary>
///     Central list of module assemblies for Wolverine handler discovery.
/// </summary>
public static class WolverineModuleDiscovery
{
    /// <summary>
    ///     All module assemblies scanned for Wolverine handlers.
    /// </summary>
    public static IReadOnlyList<Assembly> HandlerAssemblies { get; } = BuildHandlerAssemblies();

    private static ImmutableArray<Assembly> BuildHandlerAssemblies()
    {
        Assembly[] assemblies =
        [
            typeof(CreateUserCommand).Assembly,
            typeof(CreateProductsCommand).Assembly,
            typeof(CreateProductReviewCommand).Assembly,
            typeof(UploadFileCommand).Assembly,
            typeof(SubmitJobCommand).Assembly,
            typeof(CleanupExpiredInvitationsHandler).Assembly,
            typeof(CleanupOrphanedProductDataHandler).Assembly,
            typeof(SendWebhookCallbackHandler).Assembly,
            typeof(GetNotificationStreamQuery).Assembly,
            typeof(UserRegisteredEmailHandler).Assembly,
        ];

        return assemblies.Distinct().ToImmutableArray();
    }
}
