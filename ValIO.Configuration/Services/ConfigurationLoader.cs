using System.Text.Json;
using Microsoft.Extensions.Logging;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;
using ValIO.Generators.CSharp.Models;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.TypeScript.Models;
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
        var generationConfig = new GenerationConfiguration
        {
            Language = ParseGenerationLanguage(projectConfig.Language),
            OutputPath = projectConfig.OutputPath,
            Namespace = projectConfig.Namespace,
            GenerateModels = projectConfig.GenerateModels,
            GenerateClients = projectConfig.GenerateClients,
            GenerateEnums = projectConfig.GenerateModels // Enums объединены с GenerateModels
        };

        // Маппинг опций
        if (projectConfig.Options != null)
        {
            var generationOptions = new GenerationOptions
            {
                ValidationLibrary = ParseValidationLibrary(projectConfig.Options.ValidationLibrary),
                QueryWrapperLibrary = ParseQueryWrapperLibrary(projectConfig.Options.QueryWrapperLibrary)
            };
            
            // Маппинг C# опций
            if (projectConfig.Options.CSharp != null)
            {
                generationOptions.CSharpOptions = ParseCSharpOptions(projectConfig.Options.CSharp);
            }
            
            // Маппинг TypeScript опций
            if (projectConfig.Options.TypeScript != null)
            {
                generationOptions.TypeScriptOptions = ParseTypeScriptOptions(projectConfig.Options.TypeScript, projectConfig.Options);
            }
            
            // Маппинг C++/UE опций
            if (projectConfig.Options.Cpp != null)
            {
                generationOptions.CppOptions = ParseCppOptions(projectConfig.Options.Cpp);
            }
            
            generationConfig.Options = generationOptions;
        }

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
            return new CSharpGenerationOptions();

        var options = new CSharpGenerationOptions();

        // Парсинг PropertyAccessor
        if (!string.IsNullOrEmpty(config.PropertyAccessor))
        {
            options.PropertyAccessor = config.PropertyAccessor.ToLowerInvariant() switch
            {
                "init" => PropertyAccessorType.Init,
                "set" => PropertyAccessorType.Set,
                _ => PropertyAccessorType.Set
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
                "class" => ModelType.Class,
                "record" => ModelType.Record,
                "struct" => ModelType.Struct,
                "recordstruct" or "record-struct" => ModelType.RecordStruct,
                _ => ModelType.Class
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
                "modelclass" or "model-class" => QueryParametersStyle.ModelClass,
                "individualparameters" or "individual-parameters" => QueryParametersStyle.IndividualParameters,
                _ => QueryParametersStyle.IndividualParameters
            };
        }

        // Парсинг GenerateInterfaces
        if (config.GenerateInterfaces.HasValue)
        {
            options.GenerateInterfaces = config.GenerateInterfaces.Value;
        }

        // Парсинг InterfaceFileStrategy
        if (!string.IsNullOrEmpty(config.InterfaceFileStrategy))
        {
            options.InterfaceFileStrategy = config.InterfaceFileStrategy.ToLowerInvariant() switch
            {
                "samefile" or "same-file" => InterfaceFileStrategy.SameFile,
                "separatefile" or "separate-file" => InterfaceFileStrategy.SeparateFile,
                _ => InterfaceFileStrategy.SameFile
            };
        }

        // Парсинг ClientAccessModifier
        if (!string.IsNullOrEmpty(config.ClientAccessModifier))
        {
            options.ClientAccessModifier = config.ClientAccessModifier.ToLowerInvariant() switch
            {
                "public" => ValIO.Core.Ast.Enums.AccessModifier.Public,
                "internal" => ValIO.Core.Ast.Enums.AccessModifier.Internal,
                _ => ValIO.Core.Ast.Enums.AccessModifier.Public
            };
        }

        // Парсинг GenerateTraces
        if (config.GenerateTraces.HasValue)
        {
            options.GenerateTraces = config.GenerateTraces.Value;
        }

        // Парсинг GenerateMetrics
        if (config.GenerateMetrics.HasValue)
        {
            options.GenerateMetrics = config.GenerateMetrics.Value;
        }

        return options;
    }

    private static object? ParseTypeScriptOptions(ConfigTypeScriptOptions? config, ConfigOptions? parentOptions = null)
    {
        if (config == null)
            return null;

        var options = new TypeScriptGenerationOptions();

        // Парсинг ModelFormat
        if (!string.IsNullOrEmpty(config.ModelFormat))
        {
            options.ModelFormat = config.ModelFormat.ToLowerInvariant() switch
            {
                "interfaces" => TypeScriptModelFormat.Interfaces,
                "validationschemas" or "validation-schemas" => TypeScriptModelFormat.ValidationSchemas,
                _ => TypeScriptModelFormat.Interfaces
            };
        }

        // Парсинг ValidationLibrary
        if (!string.IsNullOrEmpty(config.ValidationLibrary))
        {
            options.ValidationLibrary = config.ValidationLibrary.ToLowerInvariant() switch
            {
                "zod" => ValidationLibrary.Zod,
                "yup" => ValidationLibrary.Yup,
                "valli" => ValidationLibrary.Valli,
                _ => (ValidationLibrary?)null
            };
        }
        else if (!string.IsNullOrEmpty(parentOptions?.ValidationLibrary))
        {
            // Fallback на родительские опции
            options.ValidationLibrary = parentOptions.ValidationLibrary.ToLowerInvariant() switch
            {
                "zod" => ValidationLibrary.Zod,
                "yup" => ValidationLibrary.Yup,
                "valli" => ValidationLibrary.Valli,
                _ => (ValidationLibrary?)null
            };
        }

        if (!string.IsNullOrWhiteSpace(config.HttpTransports))
        {
            var parsed = ParseHttpTransports(config.HttpTransports.Trim());
            options.HttpTransports = parsed == TypeScriptHttpTransports.None
                ? TypeScriptHttpTransports.Fetch
                : parsed;
        }

        // Парсинг UseTanStackQuery
        if (config.UseTanStackQuery.HasValue)
        {
            options.UseTanStackQuery = config.UseTanStackQuery.Value;
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
                "enum" => TypeScriptEnumFormat.Enum,
                "uniontype" or "union-type" or "union" => TypeScriptEnumFormat.UnionType,
                _ => TypeScriptEnumFormat.Enum
            };
        }

        // Парсинг UseStrictNullChecks
        if (config.UseStrictNullChecks.HasValue)
        {
            options.UseStrictNullChecks = config.UseStrictNullChecks.Value;
        }

        // Парсинг UseBigInt
        if (config.UseBigInt.HasValue)
        {
            options.UseBigInt = config.UseBigInt.Value;
        }

        // Парсинг Axios опций
        if (config.Axios != null)
        {
            options.Axios = new AxiosOptions
            {
                CustomInstancePath = config.Axios.CustomInstancePath,
                CustomInstanceName = config.Axios.CustomInstanceName
            };
        }

        if (config.Fetch != null)
        {
            options.Fetch = new FetchOptions
            {
                CustomFetcherPath = config.Fetch.CustomFetcherPath,
                CustomFetcherName = config.Fetch.CustomFetcherName
            };
        }

        // Парсинг QueryParametersStyle (оставлено для обратной совместимости, но не используется)
        // QueryParametersStyle не используется для TypeScript, query параметры всегда передаются как отдельные параметры

        return options;
    }

    private static TypeScriptHttpTransports ParseHttpTransports(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        if (v is "both" or "axios,fetch" or "fetch,axios")
            return TypeScriptHttpTransports.Fetch | TypeScriptHttpTransports.Axios;

        TypeScriptHttpTransports result = TypeScriptHttpTransports.None;
        foreach (var part in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part switch
            {
                "fetch" => TypeScriptHttpTransports.Fetch,
                "axios" => TypeScriptHttpTransports.Axios,
                _ => TypeScriptHttpTransports.None
            };
        }

        return result;
    }

    /// <summary>
    /// Парсит C++/Unreal Engine опции из конфигурации
    /// </summary>
    private static CppGenerationOptions ParseCppOptions(ConfigCppOptions? config)
    {
        if (config == null)
            return new CppGenerationOptions();

        var options = new CppGenerationOptions();

        if (!string.IsNullOrEmpty(config.ModuleApiMacro))
            options.ModuleApiMacro = config.ModuleApiMacro;

        if (!string.IsNullOrEmpty(config.BlueprintCategory))
            options.BlueprintCategory = config.BlueprintCategory;

        if (!string.IsNullOrEmpty(config.Copyright))
            options.Copyright = config.Copyright;

        if (!string.IsNullOrEmpty(config.BaseAsyncActionClassName))
            options.BaseAsyncActionClassName = config.BaseAsyncActionClassName;

        if (!string.IsNullOrEmpty(config.BaseAsyncActionIncludePath))
            options.BaseAsyncActionIncludePath = config.BaseAsyncActionIncludePath;

        options.AddUnrealEngineIncludesInHeaders = config.AddUnrealEngineIncludesInHeaders;

        return options;
    }
}
