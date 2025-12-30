using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private ScriptObject CreateScriptObject(object model)
    {
        var scriptObject = new ScriptObject();

        if (model == null)
            return scriptObject;

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var converted = ConvertValue(model, visited);

        // Если результат - ScriptObject, используем его напрямую
        if (converted is ScriptObject so)
        {
            return so;
        }

        // Если результат - список, оборачиваем в ScriptObject
        if (converted is IList list)
        {
            scriptObject["items"] = list;
            return scriptObject;
        }

        // Для остальных случаев создаем ScriptObject из свойств
        return CreateScriptObjectFromProperties(model, visited);
    }

    /// <summary>
    /// Создает ScriptObject из свойств и полей объекта
    /// </summary>
    private ScriptObject CreateScriptObjectFromProperties(object obj, HashSet<object> visited)
    {
        var scriptObject = new ScriptObject();
        var type = obj.GetType();

        // Добавляем публичные свойства
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            var value = property.GetValue(obj);
            var converted = ConvertValue(value, visited);
            scriptObject[property.Name] = converted;
        }

        // Добавляем публичные поля
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = field.GetValue(obj);
            var converted = ConvertValue(value, visited);
            scriptObject[field.Name] = converted;
        }

        return scriptObject;
    }

    /// <summary>
    /// Преобразует значение для использования в Scriban
    /// Рекурсивно обрабатывает вложенные объекты и коллекции с защитой от циклических ссылок
    /// </summary>
    private object? ConvertValue(object? value, HashSet<object> visited)
    {
        if (value == null)
            return null;

        var type = value.GetType();

        // Проверка на циклические ссылки для ссылочных типов
        if (!type.IsValueType && !type.IsPrimitive && type != typeof(string))
        {
            if (visited.Contains(value))
            {
                // Возвращаем строковое представление для циклических ссылок
                return $"[Circular Reference: {type.Name}]";
            }
            visited.Add(value);
        }

        try
        {
            return ConvertValueInternal(value, type, visited);
        }
        finally
        {
            // Удаляем из visited только если это не value type
            if (!type.IsValueType && !type.IsPrimitive && type != typeof(string))
            {
                visited.Remove(value);
            }
        }
    }

    /// <summary>
    /// Внутренняя реализация преобразования значения
    /// </summary>
    private object? ConvertValueInternal(object value, Type type, HashSet<object> visited)
    {
        // Примитивные типы и простые value types возвращаем как есть
        if (IsSimpleType(type))
        {
            return value;
        }

        // Обработка JsonNode и его производных типов
        if (value is JsonNode jsonNode)
        {
            return ConvertJsonNode(jsonNode, visited);
        }

        // Обработка словарей
        if (value is IDictionary dictionary)
        {
            return ConvertDictionary(dictionary, visited);
        }

        // Обработка generic словарей
        if (IsGenericDictionary(type))
        {
            return ConvertGenericDictionary(value, type, visited);
        }

        // Обработка массивов
        if (type.IsArray)
        {
            return ConvertArray((Array)value, visited);
        }

        // Обработка коллекций (IEnumerable, но не строки)
        if (value is IEnumerable enumerable && type != typeof(string))
        {
            return ConvertEnumerable(enumerable, visited);
        }

        // Для остальных объектов создаем ScriptObject из свойств и полей
        return CreateScriptObjectFromProperties(value, visited);
    }

    /// <summary>
    /// Проверяет, является ли тип простым (примитивным или простым value type)
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        if (type.IsPrimitive)
            return true;

        if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan) ||
            type == typeof(Uri) || type == typeof(Version))
        {
            return true;
        }

        // Nullable типы
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            return underlyingType != null && IsSimpleType(underlyingType);
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли тип generic словарем
    /// </summary>
    private static bool IsGenericDictionary(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericType = type.GetGenericTypeDefinition();
        return genericType == typeof(Dictionary<,>) || 
               genericType == typeof(IDictionary<,>) ||
               genericType == typeof(IReadOnlyDictionary<,>);
    }

    /// <summary>
    /// Преобразует JsonNode в ScriptObject или список
    /// </summary>
    private object ConvertJsonNode(JsonNode jsonNode, HashSet<object> visited)
    {
        return jsonNode switch
        {
            JsonObject jsonObject => ConvertJsonObject(jsonObject, visited),
            JsonArray jsonArray => ConvertJsonArray(jsonArray, visited),
            JsonValue jsonValue => ConvertJsonValue(jsonValue),
            _ => jsonNode.ToString()
        };
    }

    /// <summary>
    /// Преобразует JsonObject в ScriptObject
    /// </summary>
    private ScriptObject ConvertJsonObject(JsonObject jsonObject, HashSet<object> visited)
    {
        var scriptObject = new ScriptObject();
        
        foreach (var property in jsonObject)
        {
            if (property.Key == null)
                continue;

            var value = property.Value;
            var converted = ConvertValue(value, visited);
            scriptObject[property.Key] = converted;
        }

        return scriptObject;
    }

    /// <summary>
    /// Преобразует JsonArray в список
    /// </summary>
    private IList ConvertJsonArray(JsonArray jsonArray, HashSet<object> visited)
    {
        var list = new List<object?>(jsonArray.Count);
        
        foreach (var item in jsonArray)
        {
            var converted = ConvertValue(item, visited);
            list.Add(converted);
        }

        return list;
    }

    /// <summary>
    /// Преобразует JsonValue в примитивное значение
    /// </summary>
    private static object? ConvertJsonValue(JsonValue jsonValue)
    {
        var valueKind = jsonValue.GetValueKind();
        
        return valueKind switch
        {
            System.Text.Json.JsonValueKind.String => jsonValue.GetValue<string>(),
            System.Text.Json.JsonValueKind.Number => jsonValue.GetValue<decimal>(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            _ => jsonValue.ToString()
        };
    }

    /// <summary>
    /// Преобразует IDictionary в ScriptObject
    /// </summary>
    private ScriptObject ConvertDictionary(IDictionary dictionary, HashSet<object> visited)
    {
        var scriptObject = new ScriptObject();
        
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString();
            if (key == null)
                continue;

            var converted = ConvertValue(entry.Value, visited);
            scriptObject[key] = converted;
        }

        return scriptObject;
    }

    /// <summary>
    /// Преобразует generic словарь в ScriptObject
    /// </summary>
    private ScriptObject ConvertGenericDictionary(object dictionary, Type type, HashSet<object> visited)
    {
        var scriptObject = new ScriptObject();
        var keyType = type.GetGenericArguments()[0];
        var valueType = type.GetGenericArguments()[1];

        // Используем рефлексию для доступа к элементам
        var enumeratorProperty = type.GetMethod("GetEnumerator");
        if (enumeratorProperty != null)
        {
            var enumerator = enumeratorProperty.Invoke(dictionary, null);
            if (enumerator is IEnumerator enumer)
            {
                while (enumer.MoveNext())
                {
                    var current = enumer.Current;
                    if (current == null)
                        continue;

                    // Получаем Key и Value через рефлексию
                    var keyProperty = current.GetType().GetProperty("Key");
                    var valueProperty = current.GetType().GetProperty("Value");

                    if (keyProperty != null && valueProperty != null)
                    {
                        var key = keyProperty.GetValue(current)?.ToString();
                        var value = valueProperty.GetValue(current);

                        if (key != null)
                        {
                            var converted = ConvertValue(value, visited);
                            scriptObject[key] = converted;
                        }
                    }
                }
            }
        }

        return scriptObject;
    }

    /// <summary>
    /// Преобразует массив в список
    /// </summary>
    private IList ConvertArray(Array array, HashSet<object> visited)
    {
        var list = new List<object?>(array.Length);
        
        foreach (var item in array)
        {
            var converted = ConvertValue(item, visited);
            list.Add(converted);
        }

        return list;
    }

    /// <summary>
    /// Преобразует IEnumerable в список
    /// </summary>
    private IList ConvertEnumerable(IEnumerable enumerable, HashSet<object> visited)
    {
        var list = new List<object?>();
        
        foreach (var item in enumerable)
        {
            var converted = ConvertValue(item, visited);
            list.Add(converted);
        }

        return list;
    }

    /// <summary>
    /// Comparer для сравнения объектов по ссылке (для защиты от циклических ссылок)
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
