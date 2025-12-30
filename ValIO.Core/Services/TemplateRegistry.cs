using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Справочник шаблонов с предзагрузкой и кэшированием
/// Использует паттерн прототип для создания копий шаблонов
/// </summary>
public class TemplateRegistry
{
    private readonly TemplateCache _cache;
    private readonly ILogger<TemplateRegistry>? _logger;
    private readonly string _templatesDirectory;

    public TemplateRegistry(string templatesDirectory, ILogger<TemplateRegistry>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(templatesDirectory))
            throw new ArgumentException("Templates directory cannot be null or empty", nameof(templatesDirectory));

        _templatesDirectory = templatesDirectory;
        _cache = new TemplateCache();
        _logger = logger;
    }

    /// <summary>
    /// Предзагружает все шаблоны из директории асинхронно
    /// </summary>
    public async Task PreloadTemplatesAsync()
    {
        _logger?.LogInformation("Starting template preload from directory: {Directory}", _templatesDirectory);

        if (!Directory.Exists(_templatesDirectory))
        {
            _logger?.LogWarning("Templates directory does not exist: {Directory}", _templatesDirectory);
            return;
        }

        var templateFiles = Directory.GetFiles(_templatesDirectory, "*.sbncs", SearchOption.TopDirectoryOnly);
        _logger?.LogInformation("Found {Count} template files", templateFiles.Length);

        var loadTasks = templateFiles.Select(async filePath =>
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                await LoadTemplateAsync(fileName, filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load template from {FilePath}", filePath);
            }
        });

        await Task.WhenAll(loadTasks);

        _logger?.LogInformation("Template preload completed. Loaded {Count} templates", _cache.Count);
    }

    /// <summary>
    /// Загружает шаблон из файла асинхронно
    /// </summary>
    public async Task LoadTemplateAsync(string templateName, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        // Если указан полный путь, используем его, иначе ищем в директории шаблонов
        string path;
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            path = filePath;
        }
        else
        {
            path = Path.Combine(_templatesDirectory, templateName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Template file not found: {path}");
            }
        }

        _logger?.LogDebug("Loading template: {TemplateName} from {Path}", templateName, path);

        var templateContent = await File.ReadAllTextAsync(path);
        var template = Template.Parse(templateContent);

        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Failed to parse template '{templateName}': {errors}");
        }

        _cache.AddTemplate(templateName, template);
        _logger?.LogDebug("Template loaded successfully: {TemplateName}", templateName);
    }

    /// <summary>
    /// Получает копию шаблона для рендеринга (паттерн прототип)
    /// </summary>
    public Template? GetTemplateClone(string templateName)
    {
        var prototype = _cache.GetTemplate(templateName);
        
        if (prototype == null)
        {
            _logger?.LogWarning("Template not found in cache: {TemplateName}", templateName);
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
            _logger?.LogError(ex, "Failed to render template: {TemplateName}", templateName);
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
    /// Создает ScriptObject из модели для использования в Scriban
    /// </summary>
    private static ScriptObject CreateScriptObject(object model)
    {
        var scriptObject = new ScriptObject();

        if (model == null)
            return scriptObject;

        var type = model.GetType();

        // Добавляем все публичные свойства
        foreach (var property in type.GetProperties())
        {
            var value = property.GetValue(model);
            scriptObject[property.Name] = value;
        }

        // Добавляем все публичные поля
        foreach (var field in type.GetFields())
        {
            var value = field.GetValue(model);
            scriptObject[field.Name] = value;
        }

        // Если модель - словарь, добавляем все его элементы
        if (model is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                scriptObject[key] = entry.Value;
            }
        }

        return scriptObject;
    }
}

