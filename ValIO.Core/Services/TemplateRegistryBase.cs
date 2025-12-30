using System.Reflection;
using Microsoft.Extensions.Logging;
using RazorLight;
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
    protected readonly RazorLightEngine _razorEngine;

    protected TemplateRegistryBase(ITemplateCache cache, ILogger logger, Assembly? operatingAssembly = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Инициализируем RazorLight engine с правильной конфигурацией
        // Используем переданную сборку или entry assembly для загрузки metadata references
        var assembly = operatingAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        
        _razorEngine = new RazorLightEngineBuilder()
            .UseMemoryCachingProvider()
            .SetOperatingAssembly(assembly)
            .Build();
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
    /// Рендерит шаблон с использованием кэшированного шаблона
    /// </summary>
    public async Task<string> RenderAsync(string templateName, object model)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var templateContent = _cache.GetTemplate(templateName);
        
        if (templateContent == null)
        {
            throw new InvalidOperationException($"Template '{templateName}' not found in cache. Make sure to preload templates first.");
        }

        try
        {
            // Используем ключ шаблона для кэширования в RazorLight
            var templateKey = templateName;
            var result = await _razorEngine.CompileRenderStringAsync(templateKey, templateContent, model);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template: {TemplateName}", templateName);
            throw new InvalidOperationException($"Failed to render template '{templateName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Рендерит шаблон синхронно (для обратной совместимости)
    /// </summary>
    public string Render(string templateName, object model)
    {
        return RenderAsync(templateName, model).GetAwaiter().GetResult();
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
        // RazorLight валидирует шаблон при компиляции, поэтому здесь просто кэшируем содержимое
        _cache.AddTemplate(templateName, templateContent);
        _logger.LogDebug("Template cached successfully: {TemplateName}", templateName);
        
        return Task.CompletedTask;
    }
}
