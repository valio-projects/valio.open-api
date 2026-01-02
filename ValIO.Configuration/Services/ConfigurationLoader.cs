using System.Text.Json;
using Microsoft.Extensions.Logging;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;
using ValIO.Generators.CSharp.Models;
using YamlDotNet.Serialization;

namespace ValIO.Configuration.Services;

/// <summary>
/// Загрузчик конфигурации
/// </summary>
public class ConfigurationLoader : IConfigurationLoader
{
    private readonly ILogger<ConfigurationLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationLoader(ILogger<ConfigurationLoader> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder().Build();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<GenerationConfiguration> LoadFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: {filePath}");

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = await File.ReadAllTextAsync(filePath);

        return extension switch
        {
            ".json" => LoadFromJson(content),
            ".yaml" or ".yml" => LoadFromYaml(content),
            _ => throw new NotSupportedException($"Configuration file format not supported: {extension}")
        };
    }

    public GenerationConfiguration LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content cannot be null or empty", nameof(json));

        try
        {
            var config = JsonSerializer.Deserialize<ValIOConfig>(json, _jsonOptions);
            if (config == null)
                throw new InvalidOperationException("Failed to deserialize configuration");

            return MapToGenerationConfiguration(config);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON configuration");
            throw new InvalidOperationException("Invalid JSON configuration", ex);
        }
    }

    public GenerationConfiguration LoadFromYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new ArgumentException("YAML content cannot be null or empty", nameof(yaml));

        try
        {
            var config = _yamlDeserializer.Deserialize<ValIOConfig>(yaml);
            if (config == null)
                throw new InvalidOperationException("Failed to deserialize configuration");

            return MapToGenerationConfiguration(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse YAML configuration");
            throw new InvalidOperationException("Invalid YAML configuration", ex);
        }
    }

    private GenerationConfiguration MapToGenerationConfiguration(ValIOConfig config)
    {
        var generationConfig = new GenerationConfiguration
        {
            Language = ParseGenerationLanguage(config.Language),
            OutputPath = config.OutputPath,
            Namespace = config.Namespace,
            GenerateEnums = config.GenerateEnums,
            GenerateModels = config.GenerateModels,
            GenerateClients = config.GenerateClients
        };

        // Маппинг опций
        if (config.Options != null)
        {
            var generationOptions = new GenerationOptions
            {
                ValidationLibrary = ParseValidationLibrary(config.Options.ValidationLibrary),
                HttpClientLibrary = ParseHttpClientLibrary(config.Options.HttpClientLibrary),
                QueryWrapperLibrary = ParseQueryWrapperLibrary(config.Options.QueryWrapperLibrary)
            };
            
            // Маппинг C# опций
            if (config.Options.CSharp != null)
            {
                generationOptions.CSharpOptions = ParseCSharpOptions(config.Options.CSharp);
            }
            
            // Маппинг TypeScript опций
            if (config.Options.TypeScript != null)
            {
                generationOptions.TypeScriptOptions = ParseTypeScriptOptions(config.Options.TypeScript, config.Options);
            }
            
            generationConfig.Options = generationOptions;
        }

        // Маппинг организации файлов
        if (config.FileOrganization != null)
        {
            generationConfig.FileOrganization = new FileOrganization
            {
                GroupingStrategy = ParseFileGroupingStrategy(config.FileOrganization.GroupingStrategy),
                SeparateModels = config.FileOrganization.SeparateModels,
                SeparateClients = config.FileOrganization.SeparateClients,
                ModelsPath = config.FileOrganization.ModelsPath,
                ClientsPath = config.FileOrganization.ClientsPath
            };
        }

        // Маппинг конвенций именования
        if (config.NamingConvention != null)
        {
            generationConfig.NamingConvention = new NamingConventions
            {
                Models = ParseNamingConvention(config.NamingConvention.Models),
                Properties = ParseNamingConvention(config.NamingConvention.Properties),
                Methods = ParseNamingConvention(config.NamingConvention.Methods),
                Enums = ParseNamingConvention(config.NamingConvention.Enums)
            };
        }

        return generationConfig;
    }

    private static GenerationLanguage ParseGenerationLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "csharp" or "c#" => GenerationLanguage.CSharp,
            "typescript" or "ts" => GenerationLanguage.TypeScript,
            "cpp" or "c++" => GenerationLanguage.Cpp,
            _ => throw new ArgumentException($"Unknown language: {language}")
        };
    }

    private static ValidationLibrary? ParseValidationLibrary(string? library)
    {
        return library?.ToLowerInvariant() switch
        {
            "zod" => ValidationLibrary.Zod,
            "yup" => ValidationLibrary.Yup,
            "valli" => ValidationLibrary.Valli,
            null or "" => null,
            _ => throw new ArgumentException($"Unknown validation library: {library}")
        };
    }

    private static HttpClientLibrary? ParseHttpClientLibrary(string? library)
    {
        return library?.ToLowerInvariant() switch
        {
            "fetch" => HttpClientLibrary.Fetch,
            "axios" => HttpClientLibrary.Axios,
            null or "" => null,
            _ => throw new ArgumentException($"Unknown HTTP client library: {library}")
        };
    }

    private static QueryWrapperLibrary? ParseQueryWrapperLibrary(string? library)
    {
        return library?.ToLowerInvariant() switch
        {
            "tanstackquery" or "tanstack-query" or "react-query" => QueryWrapperLibrary.TanStackQuery,
            "swr" => QueryWrapperLibrary.SWR,
            null or "" => null,
            _ => throw new ArgumentException($"Unknown query wrapper library: {library}")
        };
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

    private static CSharpGenerationOptions ParseCSharpOptions(ConfigCSharpOptions? config)
    {
        if (config == null)
            return new ValIO.Generators.CSharp.Models.CSharpGenerationOptions();

        var options = new ValIO.Generators.CSharp.Models.CSharpGenerationOptions();

        // Парсинг PropertyAccessor
        if (!string.IsNullOrEmpty(config.PropertyAccessor))
        {
            options.PropertyAccessor = config.PropertyAccessor.ToLowerInvariant() switch
            {
                "init" => ValIO.Generators.CSharp.Models.PropertyAccessorType.Init,
                "set" => ValIO.Generators.CSharp.Models.PropertyAccessorType.Set,
                _ => ValIO.Generators.CSharp.Models.PropertyAccessorType.Set
            };
        }

        // Парсинг UseRequired
        if (config.UseRequired.HasValue)
        {
            options.UseRequired = config.UseRequired.Value;
        }

        // Парсинг UseSealed
        if (config.UseSealed.HasValue)
        {
            options.UseSealed = config.UseSealed.Value;
        }

        // Парсинг ModelType
        if (!string.IsNullOrEmpty(config.ModelType))
        {
            options.ModelType = config.ModelType.ToLowerInvariant() switch
            {
                "class" => ValIO.Generators.CSharp.Models.ModelType.Class,
                "record" => ValIO.Generators.CSharp.Models.ModelType.Record,
                "struct" => ValIO.Generators.CSharp.Models.ModelType.Struct,
                "recordstruct" or "record-struct" => ValIO.Generators.CSharp.Models.ModelType.RecordStruct,
                _ => ValIO.Generators.CSharp.Models.ModelType.Class
            };
        }

        // Парсинг GenerateEqualityMembers
        if (config.GenerateEqualityMembers.HasValue)
        {
            options.GenerateEqualityMembers = config.GenerateEqualityMembers.Value;
        }

        // Парсинг UseNullable
        if (config.UseNullable.HasValue)
        {
            options.UseNullable = config.UseNullable.Value;
        }

        // Парсинг GenerateValidationConstants
        if (config.GenerateValidationConstants.HasValue)
        {
            options.GenerateValidationConstants = config.GenerateValidationConstants.Value;
        }

        // Парсинг GenerateValidationAttributes
        if (config.GenerateValidationAttributes.HasValue)
        {
            options.GenerateValidationAttributes = config.GenerateValidationAttributes.Value;
        }

        // Парсинг QueryParametersStyle
        if (!string.IsNullOrEmpty(config.QueryParametersStyle))
        {
            options.QueryParametersStyle = config.QueryParametersStyle.ToLowerInvariant() switch
            {
                "modelclass" or "model-class" => ValIO.Generators.CSharp.Models.QueryParametersStyle.ModelClass,
                "individualparameters" or "individual-parameters" => ValIO.Generators.CSharp.Models.QueryParametersStyle.IndividualParameters,
                _ => ValIO.Generators.CSharp.Models.QueryParametersStyle.IndividualParameters
            };
        }

        return options;
    }

    private static ValIO.Generators.TypeScript.Models.TypeScriptGenerationOptions ParseTypeScriptOptions(ConfigTypeScriptOptions? config, ConfigOptions? parentOptions = null)
    {
        if (config == null)
            return new ValIO.Generators.TypeScript.Models.TypeScriptGenerationOptions();

        var options = new ValIO.Generators.TypeScript.Models.TypeScriptGenerationOptions();

        // Парсинг ModelFormat
        if (!string.IsNullOrEmpty(config.ModelFormat))
        {
            options.ModelFormat = config.ModelFormat.ToLowerInvariant() switch
            {
                "interfaces" => ValIO.Generators.TypeScript.Models.ModelFormat.Interfaces,
                "validationschemas" or "validation-schemas" => ValIO.Generators.TypeScript.Models.ModelFormat.ValidationSchemas,
                _ => ValIO.Generators.TypeScript.Models.ModelFormat.Interfaces
            };
        }

        // Парсинг ValidationLibrary - сначала из TypeScript опций, затем из родительских опций
        if (!string.IsNullOrEmpty(config.ValidationLibrary))
        {
            options.ValidationLibrary = ParseValidationLibrary(config.ValidationLibrary);
        }
        else if (parentOptions != null && !string.IsNullOrEmpty(parentOptions.ValidationLibrary))
        {
            options.ValidationLibrary = ParseValidationLibrary(parentOptions.ValidationLibrary);
        }

        // Парсинг HttpClientLibrary - сначала из TypeScript опций, затем из родительских опций
        if (!string.IsNullOrEmpty(config.HttpClientLibrary))
        {
            var httpClientLib = ParseHttpClientLibrary(config.HttpClientLibrary);
            if (httpClientLib.HasValue)
            {
                options.HttpClientLibrary = httpClientLib.Value;
            }
        }
        else if (parentOptions != null && !string.IsNullOrEmpty(parentOptions.HttpClientLibrary))
        {
            var httpClientLib = ParseHttpClientLibrary(parentOptions.HttpClientLibrary);
            if (httpClientLib.HasValue)
            {
                options.HttpClientLibrary = httpClientLib.Value;
            }
        }

        // Парсинг UseTanStackQuery
        if (config.UseTanStackQuery.HasValue)
        {
            options.UseTanStackQuery = config.UseTanStackQuery.Value;
        }
        else if (parentOptions != null && !string.IsNullOrEmpty(parentOptions.QueryWrapperLibrary))
        {
            // Если QueryWrapperLibrary задан на верхнем уровне, считаем что UseTanStackQuery = true
            var queryWrapper = ParseQueryWrapperLibrary(parentOptions.QueryWrapperLibrary);
            if (queryWrapper == QueryWrapperLibrary.TanStackQuery)
            {
                options.UseTanStackQuery = true;
            }
        }

        // Парсинг SerializeWithSchemas
        if (config.SerializeWithSchemas.HasValue)
        {
            options.SerializeWithSchemas = config.SerializeWithSchemas.Value;
        }

        // Парсинг DeserializeWithSchemas
        if (config.DeserializeWithSchemas.HasValue)
        {
            options.DeserializeWithSchemas = config.DeserializeWithSchemas.Value;
        }

        // Парсинг EnumFormat
        if (!string.IsNullOrEmpty(config.EnumFormat))
        {
            options.EnumFormat = config.EnumFormat.ToLowerInvariant() switch
            {
                "enum" => ValIO.Generators.TypeScript.Models.EnumFormat.Enum,
                "uniontype" or "union-type" => ValIO.Generators.TypeScript.Models.EnumFormat.UnionType,
                _ => ValIO.Generators.TypeScript.Models.EnumFormat.Enum
            };
        }

        // Парсинг UseStrictNullChecks
        if (config.UseStrictNullChecks.HasValue)
        {
            options.UseStrictNullChecks = config.UseStrictNullChecks.Value;
        }

        // Парсинг Axios опций
        if (config.Axios != null)
        {
            options.Axios = new ValIO.Generators.TypeScript.Models.AxiosOptions
            {
                CustomInstancePath = config.Axios.CustomInstancePath,
                CustomInstanceName = config.Axios.CustomInstanceName
            };
        }

        // Парсинг QueryParametersStyle
        if (!string.IsNullOrEmpty(config.QueryParametersStyle))
        {
            options.QueryParametersStyle = config.QueryParametersStyle.ToLowerInvariant() switch
            {
                "modelclass" or "model-class" => ValIO.Generators.CSharp.Models.QueryParametersStyle.ModelClass,
                "individualparameters" or "individual-parameters" => ValIO.Generators.CSharp.Models.QueryParametersStyle.IndividualParameters,
                _ => ValIO.Generators.CSharp.Models.QueryParametersStyle.IndividualParameters
            };
        }

        return options;
    }
}

