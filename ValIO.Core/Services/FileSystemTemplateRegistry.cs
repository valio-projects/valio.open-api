using Microsoft.Extensions.Logging;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация TemplateRegistry для загрузки шаблонов из файловой системы
/// </summary>
public class FileSystemTemplateRegistry : TemplateRegistryBase
{
    private readonly string _templatesDirectory;

    public FileSystemTemplateRegistry(
        string templatesDirectory,
        ITemplateCache cache,
        ILogger<FileSystemTemplateRegistry> logger)
        : base(cache, logger)
    {
        if (string.IsNullOrWhiteSpace(templatesDirectory))
            throw new ArgumentException("Templates directory cannot be null or empty", nameof(templatesDirectory));

        _templatesDirectory = templatesDirectory;
    }

    /// <summary>
    /// Предзагружает все шаблоны из директории асинхронно
    /// </summary>
    public override async Task PreloadTemplatesAsync()
    {
        _logger.LogInformation("Starting template preload from directory: {Directory}", _templatesDirectory);

        if (!Directory.Exists(_templatesDirectory))
        {
            _logger.LogWarning("Templates directory does not exist: {Directory}", _templatesDirectory);
            return;
        }

        var templateFiles = Directory.GetFiles(_templatesDirectory, "*.cshtml", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("Found {Count} template files", templateFiles.Length);

        var loadTasks = templateFiles.Select(async filePath =>
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                await LoadTemplateAsync(fileName, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template from {FilePath}", filePath);
            }
        });

        await Task.WhenAll(loadTasks);

        _logger.LogInformation("Template preload completed. Loaded {Count} templates", _cache.Count);
    }

    /// <summary>
    /// Загружает шаблон из файла асинхронно
    /// </summary>
    public override async Task LoadTemplateAsync(string templateName, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        // Если указан полный путь к файлу, используем его
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

        _logger.LogDebug("Loading template: {TemplateName} from file {Path}", templateName, path);

        var templateContent = await File.ReadAllTextAsync(path);
        await ParseAndCacheTemplateAsync(templateName, templateContent);
    }
}
