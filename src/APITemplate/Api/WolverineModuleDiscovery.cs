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
    public static IReadOnlyList<Assembly> HandlerAssemblies { get; } =
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

    /// <summary>
    ///     Assemblies whose FluentValidation validators should run in <see cref="SharedKernel.Application.Middleware.ErrorOrValidationMiddleware" />.
    /// </summary>
    public static Assembly[] ErrorOrValidationAssemblies { get; } =
    [
        typeof(CreateUserCommand).Assembly,
        typeof(CreateProductsCommand).Assembly,
        typeof(CreateProductReviewCommand).Assembly,
        typeof(UploadFileCommand).Assembly,
        typeof(SubmitJobCommand).Assembly,
    ];
}
