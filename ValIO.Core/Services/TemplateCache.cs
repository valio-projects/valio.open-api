using System.Collections.Concurrent;
using System.Linq;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Кэш для хранения содержимого Razor шаблонов
/// Thread-safe реализация
/// </summary>
public class TemplateCache : ITemplateCache
{
    private readonly ConcurrentDictionary<string, string> _templates = new();

    /// <summary>
    /// Добавляет содержимое шаблона в кэш
    /// </summary>
    public void AddTemplate(string name, string templateContent)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be null or empty", nameof(name));

        if (string.IsNullOrWhiteSpace(templateContent))
            throw new ArgumentException("Template content cannot be null or empty", nameof(templateContent));

        _templates[name] = templateContent;
    }

    /// <summary>
    /// Добавляет или обновляет шаблон в кэше
    /// </summary>
    public void AddOrUpdateTemplate(string name, string templateContent)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be null or empty", nameof(name));

        if (string.IsNullOrWhiteSpace(templateContent))
            throw new ArgumentException("Template content cannot be null or empty", nameof(templateContent));

        _templates.AddOrUpdate(name, templateContent, (key, oldValue) => templateContent);
    }

    /// <summary>
    /// Получает содержимое шаблона из кэша
    /// </summary>
    public string? GetTemplate(string name)
    {
        return _templates.TryGetValue(name, out var template) ? template : null;
    }

    /// <summary>
    /// Проверяет наличие шаблона в кэше
    /// </summary>
    public bool HasTemplate(string name)
    {
        return _templates.ContainsKey(name);
    }

    /// <summary>
    /// Получает все имена шаблонов в кэше
    /// </summary>
    public IReadOnlyCollection<string> GetTemplateNames()
    {
        return _templates.Keys.ToArray();
    }

    /// <summary>
    /// Очищает кэш
    /// </summary>
    public void Clear()
    {
        _templates.Clear();
    }

    /// <summary>
    /// Получает количество шаблонов в кэше
    /// </summary>
    public int Count => _templates.Count;
}

