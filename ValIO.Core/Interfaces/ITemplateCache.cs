namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для кэша шаблонов
/// </summary>
public interface ITemplateCache
{
    /// <summary>
    /// Добавляет содержимое шаблона в кэш
    /// </summary>
    void AddTemplate(string name, string templateContent);

    /// <summary>
    /// Добавляет или обновляет шаблон в кэше
    /// </summary>
    void AddOrUpdateTemplate(string name, string templateContent);

    /// <summary>
    /// Получает содержимое шаблона из кэша
    /// </summary>
    string? GetTemplate(string name);

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
