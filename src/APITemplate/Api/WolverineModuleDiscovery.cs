using System.Collections.Immutable;
using System.Reflection;
using BackgroundJobs.Features;
using Chatting.Features.GetNotificationStream;
using FileStorage.Features.Upload;
using Identity.Features.User;
using Identity.Handlers;
using Notifications.Features;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.Handlers;
using Reviews.Features;
using Webhooks.Features.SendWebhookCallback;

namespace APITemplate.Api;

/// <summary>
///     Central list of module assemblies for Wolverine handler discovery and ErrorOr validation middleware.
/// </summary>
public static class WolverineModuleDiscovery
{
    /// <summary>
    ///     Assemblies whose FluentValidation validators should run in <see cref="SharedKernel.Application.Middleware.ErrorOrValidationMiddleware" />.
    /// </summary>
    public static IReadOnlyList<Assembly> ErrorOrValidationAssemblies { get; } =
        ImmutableArray.Create(
            typeof(CreateUserCommand).Assembly,
            typeof(CreateProductsCommand).Assembly,
            typeof(CreateProductReviewCommand).Assembly,
            typeof(UploadFileCommand).Assembly,
            typeof(SubmitJobCommand).Assembly
        );

    /// <summary>
    ///     All module assemblies scanned for Wolverine handlers (includes <see cref="ErrorOrValidationAssemblies" /> plus handler-only assemblies).
    /// </summary>
    public static IReadOnlyList<Assembly> HandlerAssemblies { get; } = BuildHandlerAssemblies();

    private static ImmutableArray<Assembly> BuildHandlerAssemblies()
    {
        Assembly[] supplemental =
        [
            typeof(CleanupExpiredInvitationsHandler).Assembly,
            typeof(CleanupOrphanedProductDataHandler).Assembly,
            typeof(SendWebhookCallbackHandler).Assembly,
            typeof(GetNotificationStreamQuery).Assembly,
            typeof(UserRegisteredEmailHandler).Assembly,
        ];

        return ErrorOrValidationAssemblies.Concat(supplemental).Distinct().ToImmutableArray();
    }
}
