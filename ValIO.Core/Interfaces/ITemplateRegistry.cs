namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для справочника шаблонов
/// </summary>
public interface ITemplateRegistry
{
    /// <summary>
    /// Предзагружает все шаблоны асинхронно
    /// </summary>
    Task PreloadTemplatesAsync();

    /// <summary>
    /// Загружает шаблон из файла или embedded resource асинхронно
    /// </summary>
    Task LoadTemplateAsync(string templateName, string? filePath = null);

    /// <summary>
    /// Рендерит шаблон с использованием кэшированного шаблона
    /// </summary>
    string Render(string templateName, object model);

    /// <summary>
    /// Проверяет наличие шаблона в кэше
    /// </summary>
    bool HasTemplate(string templateName);

    /// <summary>
    /// Получает все имена загруженных шаблонов
    /// </summary>
    IReadOnlyCollection<string> GetLoadedTemplateNames();
}
