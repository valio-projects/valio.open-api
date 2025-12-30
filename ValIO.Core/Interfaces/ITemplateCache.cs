using Scriban;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для кэша шаблонов
/// </summary>
public interface ITemplateCache
{
    /// <summary>
    /// Добавляет скомпилированный шаблон в кэш
    /// </summary>
    void AddTemplate(string name, Template template);

    /// <summary>
    /// Добавляет или обновляет шаблон в кэше
    /// </summary>
    void AddOrUpdateTemplate(string name, Template template);

    /// <summary>
    /// Получает шаблон из кэша (прототип)
    /// </summary>
    Template? GetTemplate(string name);

    /// <summary>
    /// Проверяет наличие шаблона в кэше
    /// </summary>
    bool HasTemplate(string name);

    /// <summary>
    /// Получает все имена шаблонов в кэше
    /// </summary>
    IReadOnlyCollection<string> GetTemplateNames();

    /// <summary>
    /// Очищает кэш
    /// </summary>
    void Clear();

    /// <summary>
    /// Получает количество шаблонов в кэше
    /// </summary>
    int Count { get; }
}
