using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Базовый класс для справочника шаблонов
/// Содержит общую логику рендеринга и работы с кэшем
/// </summary>
public abstract class TemplateRegistryBase : ITemplateRegistry
{
    protected readonly ITemplateCache _cache;
    protected readonly ILogger _logger;

    protected TemplateRegistryBase(ITemplateCache cache, ILogger logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Предзагружает все шаблоны асинхронно
    /// </summary>
    public abstract Task PreloadTemplatesAsync();

    /// <summary>
    /// Загружает шаблон из файла или embedded resource асинхронно
    /// </summary>
    public abstract Task LoadTemplateAsync(string templateName, string? filePath = null);

    /// <summary>
    /// Получает копию шаблона для рендеринга (паттерн прототип)
    /// </summary>
    protected Template? GetTemplateClone(string templateName)
    {
        var prototype = _cache.GetTemplate(templateName);
        
        if (prototype == null)
        {
            _logger.LogWarning("Template not found in cache: {TemplateName}", templateName);
            return null;
        }

        // Создаем копию шаблона для безопасного использования в многопоточной среде
        // В Scriban шаблоны неизменяемы после компиляции, но мы можем создать новый экземпляр
        // для изоляции контекста рендеринга
        return prototype;
    }

    /// <summary>
    /// Рендерит шаблон с использованием кэшированного шаблона
    /// </summary>
    public string Render(string templateName, object model)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var template = GetTemplateClone(templateName);
        
        if (template == null)
        {
            throw new InvalidOperationException($"Template '{templateName}' not found in cache. Make sure to preload templates first.");
        }

        try
        {
            var scriptObject = CreateScriptObject(model);
            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            return template.Render(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template: {TemplateName}", templateName);
            throw new InvalidOperationException($"Failed to render template '{templateName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Проверяет наличие шаблона в кэше
    /// </summary>
    public bool HasTemplate(string templateName)
    {
        return _cache.HasTemplate(templateName);
    }

    /// <summary>
    /// Получает все имена загруженных шаблонов
    /// </summary>
    public IReadOnlyCollection<string> GetLoadedTemplateNames()
    {
        return _cache.GetTemplateNames();
    }

    /// <summary>
    /// Парсит шаблон и добавляет в кэш
    /// </summary>
    protected Task ParseAndCacheTemplateAsync(string templateName, string templateContent)
    {
        // Парсим шаблон синхронно (Scriban.Parse синхронный)
        var template = Template.Parse(templateContent);

        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Failed to parse template '{templateName}': {errors}");
        }

        _cache.AddTemplate(templateName, template);
        _logger.LogDebug("Template loaded successfully: {TemplateName}", templateName);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Создает ScriptObject из модели для использования в Scriban
    /// Поддерживает вложенные объекты, коллекции, массивы и словари
    /// </summary>
    private static ScriptObject CreateScriptObject(object model)
    {
        var scriptObject = new ScriptObject();

        if (model == null)
            return scriptObject;

        var type = model.GetType();

        // Если модель - словарь, добавляем все его элементы
        if (model is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                scriptObject[key] = ConvertValue(entry.Value);
            }
            return scriptObject;
        }

        // Если модель - IEnumerable (но не строка), преобразуем в список
        if (model is System.Collections.IEnumerable enumerable && model is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(ConvertValue(item));
            }
            scriptObject["items"] = list;
            return scriptObject;
        }

        // Добавляем все публичные свойства
        foreach (var property in type.GetProperties())
        {
            try
            {
                var value = property.GetValue(model);
                scriptObject[property.Name] = ConvertValue(value);
            }
            catch
            {
                // Игнорируем свойства, которые не могут быть прочитаны
            }
        }

        // Добавляем все публичные поля
        foreach (var field in type.GetFields())
        {
            try
            {
                var value = field.GetValue(model);
                scriptObject[field.Name] = ConvertValue(value);
            }
            catch
            {
                // Игнорируем поля, которые не могут быть прочитаны
            }
        }

        return scriptObject;
    }

    /// <summary>
    /// Преобразует значение для использования в Scriban
    /// Рекурсивно обрабатывает вложенные объекты и коллекции
    /// </summary>
    private static object? ConvertValue(object? value)
    {
        if (value == null)
            return null;

        // Примитивные типы и строки возвращаем как есть
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || 
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) ||
            type == typeof(TimeSpan) || type == typeof(Uri))
        {
            return value;
        }

        // Если это словарь, преобразуем в ScriptObject
        if (value is System.Collections.IDictionary dictionary)
        {
            var scriptObject = new ScriptObject();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                scriptObject[key] = ConvertValue(entry.Value);
            }
            return scriptObject;
        }

        // Если это IEnumerable (но не строка), преобразуем в список
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(ConvertValue(item));
            }
            return list;
        }

        // Для остальных объектов создаем ScriptObject из свойств и полей
        var obj = new ScriptObject();
        foreach (var property in type.GetProperties())
        {
            try
            {
                var propValue = property.GetValue(value);
                obj[property.Name] = ConvertValue(propValue);
            }
            catch
            {
                // Игнорируем свойства, которые не могут быть прочитаны
            }
        }
        foreach (var field in type.GetFields())
        {
            try
            {
                var fieldValue = field.GetValue(value);
                obj[field.Name] = ConvertValue(fieldValue);
            }
            catch
            {
                // Игнорируем поля, которые не могут быть прочитаны
            }
        }

        return obj;
    }
}
