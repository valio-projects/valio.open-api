using Scriban;
using Scriban.Runtime;

namespace ValIO.Core.Services;

/// <summary>
/// Кэш для хранения скомпилированных Scriban шаблонов
/// Использует паттерн прототип для создания копий шаблонов
/// </summary>
public class TemplateCache
{
    private readonly Dictionary<string, Template> _compiledTemplates = new();

    /// <summary>
    /// Добавляет скомпилированный шаблон в кэш
    /// </summary>
    public void AddTemplate(string name, Template template)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be null or empty", nameof(name));

        if (template == null)
            throw new ArgumentNullException(nameof(template));

        _compiledTemplates[name] = template;
    }

    /// <summary>
    /// Получает шаблон из кэша (прототип)
    /// </summary>
    public Template? GetTemplate(string name)
    {
        return _compiledTemplates.TryGetValue(name, out var template) ? template : null;
    }

    /// <summary>
    /// Проверяет наличие шаблона в кэше
    /// </summary>
    public bool HasTemplate(string name)
    {
        return _compiledTemplates.ContainsKey(name);
    }

    /// <summary>
    /// Получает все имена шаблонов в кэше
    /// </summary>
    public IReadOnlyCollection<string> GetTemplateNames()
    {
        return _compiledTemplates.Keys;
    }

    /// <summary>
    /// Очищает кэш
    /// </summary>
    public void Clear()
    {
        _compiledTemplates.Clear();
    }

    /// <summary>
    /// Получает количество шаблонов в кэше
    /// </summary>
    public int Count => _compiledTemplates.Count;
}

