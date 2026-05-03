using System.Collections.Concurrent;
using System.Reflection;
using BuildingBlocks.Application.Errors;
using Fluid;
using Notifications.Contracts;
using NTF = Notifications.Errors.ErrorCatalog;

namespace Notifications.Services;

/// <summary>
///     Renders Liquid email templates embedded as assembly resources using the Fluid library.
///     Parsed templates are cached in a <see cref="ConcurrentDictionary{TKey,TValue}" /> to avoid
///     repeated parsing across requests.
/// </summary>
public sealed class FluidEmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly FluidParser Parser = new();
    private static readonly Assembly ResourceAssembly = typeof(FluidEmailTemplateRenderer).Assembly;

    // Bounded by the fixed set of embedded resource names (EmailTemplateNames constants).
    // Dynamic template names are not supported and would cause unbounded growth.
    private static readonly ConcurrentDictionary<string, Lazy<Task<IFluidTemplate>>> TemplateCache =
        new(StringComparer.Ordinal);

    /// <summary>Retrieves (or parses and caches) the named template and renders it against <paramref name="model" />.</summary>
    public async Task<string> RenderAsync(
        string templateName,
        object model,
        CancellationToken ct = default
    )
    {
        IFluidTemplate template = await GetOrParseTemplateAsync(templateName);
        TemplateContext context = new(model);
        return await template.RenderAsync(context);
    }

    private static Task<IFluidTemplate> GetOrParseTemplateAsync(string templateName)
    {
        Lazy<Task<IFluidTemplate>> lazyTemplate = TemplateCache.GetOrAdd(
            templateName,
            static name => new Lazy<Task<IFluidTemplate>>(
                () => ParseTemplateAsync(name),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return AwaitTemplateAsync(templateName, lazyTemplate);
    }

    private static async Task<IFluidTemplate> AwaitTemplateAsync(
        string templateName,
        Lazy<Task<IFluidTemplate>> lazyTemplate
    )
    {
        try
        {
            return await lazyTemplate.Value;
        }
        catch
        {
            TemplateCache.TryRemove(
                new KeyValuePair<string, Lazy<Task<IFluidTemplate>>>(templateName, lazyTemplate)
            );
            throw;
        }
    }

    private static async Task<IFluidTemplate> ParseTemplateAsync(string templateName)
    {
        string templateContent = await LoadTemplateAsync(templateName);

        if (!Parser.TryParse(templateContent, out IFluidTemplate? template, out string? error))
        {
            throw new AppException(
                $"Failed to parse email template '{templateName}': {error}",
                NTF.Templates.ParseFailed
            );
        }

        return template;
    }

    private static async Task<string> LoadTemplateAsync(string templateName)
    {
        string resourceName = $"{ResourceAssembly.GetName().Name}.{templateName}.liquid";

        await using Stream stream =
            ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new AppException(
                $"Email template '{templateName}' not found as embedded resource '{resourceName}'.",
                NTF.Templates.NotFound
            );

        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync();
    }
}
