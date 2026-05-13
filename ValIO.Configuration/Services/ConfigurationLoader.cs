using System.Text.Json;
using Microsoft.Extensions.Logging;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using YamlDotNet.Serialization;

namespace ValIO.Configuration.Services;

/// <summary>
/// Загрузчик конфигурации
/// </summary>
public class ConfigurationLoader : IConfigurationLoader
{
    private readonly ILogger<ConfigurationLoader> _logger;
    private readonly IGeneratorRegistry _registry;
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationLoader(ILogger<ConfigurationLoader> logger, IGeneratorRegistry registry)
    {
        _logger = logger;
        _registry = registry;
        _yamlDeserializer = new DeserializerBuilder().Build();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ValIOConfig> LoadConfigFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: {filePath}");

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = await File.ReadAllTextAsync(filePath);

        return extension switch
        {
            ".json" => LoadConfigFromJson(content),
            ".yaml" or ".yml" => LoadConfigFromYaml(content),
            _ => throw new NotSupportedException($"Configuration file format not supported: {extension}")
        };
    }

    public ValIOConfig LoadConfigFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content cannot be null or empty", nameof(json));

        try
        {
            var config = JsonSerializer.Deserialize<ValIOConfig>(json, _jsonOptions);
            if (config == null)
                throw new InvalidOperationException("Failed to deserialize configuration");

            return config;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON configuration");
            throw new InvalidOperationException("Invalid JSON configuration", ex);
        }
    }

    public ValIOConfig LoadConfigFromYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new ArgumentException("YAML content cannot be null or empty", nameof(yaml));

        try
        {
            var config = _yamlDeserializer.Deserialize<ValIOConfig>(yaml);
            if (config == null)
                throw new InvalidOperationException("Failed to deserialize configuration");

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse YAML configuration");
            throw new InvalidOperationException("Invalid YAML configuration", ex);
        }
    }

    public GenerationConfiguration MapToGenerationConfiguration(ProjectConfig projectConfig)
    {
        // Определить язык и найти плагин
        var languageId = !string.IsNullOrWhiteSpace(projectConfig.Language)
            ? projectConfig.Language
            : "typescript";

        var plugin = _registry.FindPlugin(languageId)
            ?? throw new InvalidOperationException(
                $"Unknown language: '{languageId}'. Available: {string.Join(", ", _registry.AllPlugins.Select(p => p.LanguageId))}");

        // Делегировать парсинг опций плагину
        var rawOptions = projectConfig.Options ?? new Dictionary<string, JsonElement>();
        var pluginOptions = plugin.OptionsDescriptor.ParseOptions(rawOptions, out var optionErrors);
        if (optionErrors.Count > 0)
            throw new InvalidOperationException(
                $"Invalid options for '{languageId}': {string.Join(", ", optionErrors)}");

        var generationConfig = new GenerationConfiguration
        {
            LanguageId = languageId,
            PluginOptions = pluginOptions,
            OutputPath = projectConfig.OutputPath,
            Namespace = projectConfig.Namespace,
            GenerateModels = projectConfig.GenerateModels,
            GenerateClients = projectConfig.GenerateClients,
            GenerateEnums = projectConfig.GenerateModels // Enums объединены с GenerateModels
        };

        // Маппинг организации файлов
        if (projectConfig.FileOrganization != null)
        {
            generationConfig.FileOrganization = new FileOrganization
            {
                GroupingStrategy = ParseFileGroupingStrategy(projectConfig.FileOrganization.GroupingStrategy),
                SeparateModels = projectConfig.FileOrganization.SeparateModels,
                SeparateClients = projectConfig.FileOrganization.SeparateClients,
                ModelsPath = projectConfig.FileOrganization.ModelsPath,
                ClientsPath = projectConfig.FileOrganization.ClientsPath
            };
        }

        // Маппинг конвенций именования
        if (projectConfig.NamingConvention != null)
        {
            generationConfig.NamingConvention = new NamingConventions
            {
                Models = ParseNamingConvention(projectConfig.NamingConvention.Models),
                Properties = ParseNamingConvention(projectConfig.NamingConvention.Properties),
                Methods = ParseNamingConvention(projectConfig.NamingConvention.Methods),
                Enums = ParseNamingConvention(projectConfig.NamingConvention.Enums)
            };
        }

        return generationConfig;
    }

    private static FileGroupingStrategy ParseFileGroupingStrategy(string? strategy)
    {
        return strategy?.ToLowerInvariant() switch
        {
            "bytag" or "by-tag" => FileGroupingStrategy.ByTag,
            "byroute" or "by-route" => FileGroupingStrategy.ByRoute,
            "singlefile" or "single-file" => FileGroupingStrategy.SingleFile,
            _ => FileGroupingStrategy.SingleFile
        };
    }

    private static NamingConvention ParseNamingConvention(string? convention)
    {
        return convention?.ToLowerInvariant() switch
        {
            "pascalcase" or "pascal-case" => NamingConvention.PascalCase,
            "camelcase" or "camel-case" => NamingConvention.CamelCase,
            "snakecase" or "snake-case" => NamingConvention.SnakeCase,
            "kebabcase" or "kebab-case" => NamingConvention.KebabCase,
            _ => NamingConvention.PascalCase
        };
    }
}
