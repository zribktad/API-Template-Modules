using System.Collections.Concurrent;
using System.Reflection;
using APITemplate.Application.Common.Email;
using Fluid;

namespace APITemplate.Infrastructure.Email;

/// <summary>
/// Renders Liquid email templates embedded as assembly resources using the Fluid library.
/// Parsed templates are cached in a <see cref="ConcurrentDictionary{TKey,TValue}"/> to avoid
/// repeated parsing across requests.
/// </summary>
public sealed class FluidEmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly FluidParser Parser = new();
    private static readonly Assembly ResourceAssembly = typeof(FluidEmailTemplateRenderer).Assembly;
    private static readonly ConcurrentDictionary<string, IFluidTemplate> TemplateCache = new();

    /// <summary>Retrieves (or parses and caches) the named template and renders it against <paramref name="model"/>.</summary>
    public async Task<string> RenderAsync(
        string templateName,
        object model,
        CancellationToken ct = default
    )
    {
        var template = await GetOrParseTemplateAsync(templateName);
        var context = new TemplateContext(model);
        return await template.RenderAsync(context);
    }

    private static async Task<IFluidTemplate> GetOrParseTemplateAsync(string templateName)
    {
        if (TemplateCache.TryGetValue(templateName, out var cached))
            return cached;

        var templateContent = await LoadTemplateAsync(templateName);

        if (!Parser.TryParse(templateContent, out var template, out var error))
            throw new InvalidOperationException(
                $"Failed to parse email template '{templateName}': {error}"
            );

        TemplateCache.TryAdd(templateName, template);
        return template;
    }

    private static async Task<string> LoadTemplateAsync(string templateName)
    {
        var resourceName =
            $"{ResourceAssembly.GetName().Name}.Email.Templates.{templateName}.liquid";

        await using var stream =
            ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Email template '{templateName}' not found as embedded resource '{resourceName}'."
            );

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
