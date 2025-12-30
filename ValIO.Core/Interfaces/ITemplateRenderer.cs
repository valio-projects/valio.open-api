namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга шаблонов кода
/// </summary>
public interface ITemplateRenderer
{
    string Render(string template, object model);
    Task<string> RenderFileAsync(string templatePath, object model);
}

