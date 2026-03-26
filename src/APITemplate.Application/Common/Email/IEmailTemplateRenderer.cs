namespace APITemplate.Application.Common.Email;

/// <summary>
/// Application-layer abstraction for rendering HTML email bodies from named templates and a view model.
/// Decouples notification handlers from the templating engine (Razor, Scriban, Liquid, etc.).
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders the template identified by <paramref name="templateName"/> using the supplied
    /// <paramref name="model"/> and returns the resulting HTML string.
    /// </summary>
    Task<string> RenderAsync(string templateName, object model, CancellationToken ct = default);
}
