using RazorLight;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация рендеринга шаблонов на основе RazorLight
/// </summary>
public class TemplateRenderer : ITemplateRenderer
{
    private readonly RazorLightEngine _razorEngine;

    public TemplateRenderer()
    {
        _razorEngine = new RazorLightEngineBuilder()
            .UseMemoryCachingProvider()
            .Build();
    }

    /// <summary>
    /// Рендеринг шаблона из строки
    /// </summary>
    public string Render(string template, object model)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be null or empty", nameof(template));

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        try
        {
            var templateKey = Guid.NewGuid().ToString();
            var result = _razorEngine.CompileRenderStringAsync(templateKey, template, model).GetAwaiter().GetResult();
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to render template: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Рендеринг шаблона из файла
    /// </summary>
    public async Task<string> RenderFileAsync(string templatePath, object model)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("Template path cannot be null or empty", nameof(templatePath));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template file not found: {templatePath}");

        var templateContent = await File.ReadAllTextAsync(templatePath);
        return Render(templateContent, model);
    }
}

