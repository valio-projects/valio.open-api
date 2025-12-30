using System.Reflection;
using Microsoft.Extensions.Logging;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация TemplateRegistry для загрузки шаблонов из embedded resources
/// </summary>
public class EmbeddedResourceTemplateRegistry : TemplateRegistryBase
{
    private readonly Assembly _resourceAssembly;
    private readonly string _resourceNamespace;

    public EmbeddedResourceTemplateRegistry(
        Assembly resourceAssembly,
        string resourceNamespace,
        ITemplateCache cache,
        ILogger<EmbeddedResourceTemplateRegistry> logger)
        : base(cache, logger, resourceAssembly) // Передаем resourceAssembly как operatingAssembly для RazorLight
    {
        _resourceAssembly = resourceAssembly ?? throw new ArgumentNullException(nameof(resourceAssembly));
        _resourceNamespace = resourceNamespace ?? throw new ArgumentNullException(nameof(resourceNamespace));
    }

    /// <summary>
    /// Предзагружает все шаблоны из embedded resources асинхронно
    /// </summary>
    public override async Task PreloadTemplatesAsync()
    {
        _logger.LogInformation("Starting template preload from embedded resources: {Namespace}", _resourceNamespace);

        var resourceNames = _resourceAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(_resourceNamespace) && name.EndsWith(".cshtml"))
            .ToArray();

        _logger.LogInformation("Found {Count} template resources", resourceNames.Length);

        var loadTasks = resourceNames.Select(async resourceName =>
        {
            try
            {
                // Извлекаем имя шаблона из полного имени ресурса
                var templateName = ExtractTemplateNameFromResourceName(resourceName);
                await LoadTemplateFromResourceAsync(templateName, resourceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template from resource {ResourceName}", resourceName);
            }
        });

        await Task.WhenAll(loadTasks);

        _logger.LogInformation("Template preload completed. Loaded {Count} templates", _cache.Count);
    }

    /// <summary>
    /// Загружает шаблон из embedded resource асинхронно
    /// </summary>
    public override async Task LoadTemplateAsync(string templateName, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        // Если указан путь к файлу, игнорируем его (используем только embedded resources)
        var resourceName = BuildResourceName(templateName);
        await LoadTemplateFromResourceAsync(templateName, resourceName);
    }

    /// <summary>
    /// Загружает шаблон из embedded resource
    /// </summary>
    private async Task LoadTemplateFromResourceAsync(string templateName, string resourceName)
    {
        _logger.LogDebug("Loading template: {TemplateName} from resource {ResourceName}", templateName, resourceName);

        await using var stream = _resourceAssembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly {_resourceAssembly.FullName}");
        }

        using var reader = new StreamReader(stream);
        var templateContent = await reader.ReadToEndAsync();
        await ParseAndCacheTemplateAsync(templateName, templateContent);
    }

    /// <summary>
    /// Извлекает имя шаблона из полного имени ресурса
    /// </summary>
    private string ExtractTemplateNameFromResourceName(string resourceName)
    {
        if (string.IsNullOrEmpty(_resourceNamespace))
            return Path.GetFileName(resourceName);

        // Формат: Namespace.Templates.template-name.cshtml
        // Извлекаем: template-name.cshtml
        // Убираем префикс namespace
        if (resourceName.StartsWith(_resourceNamespace + "."))
        {
            var withoutNamespace = resourceName.Substring(_resourceNamespace.Length + 1);
            // Если есть подпапка Templates, убираем её тоже
            if (withoutNamespace.StartsWith("Templates."))
            {
                return withoutNamespace.Substring("Templates.".Length);
            }
            return withoutNamespace;
        }

        // Если формат неожиданный, берем последние части
        var parts = resourceName.Split('.');
        if (parts.Length >= 2)
        {
            // Берем последние две части (имя файла и расширение)
            return $"{parts[^2]}.{parts[^1]}";
        }
        return Path.GetFileName(resourceName);
    }

    /// <summary>
    /// Строит полное имя ресурса из имени шаблона
    /// </summary>
    private string BuildResourceName(string templateName)
    {
        // Формат: Namespace.Templates.template-name.cshtml
        // LogicalName в csproj: $(MSBuildProjectName).Templates.%(Filename)%(Extension)
        // Получается: ValIO.Generators.CSharp.Templates.csharp-client.cshtml
        return $"{_resourceNamespace}.{templateName}";
    }
}
