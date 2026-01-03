using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Models;
using System.Linq;
using System.Text;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Базовый генератор кода для TypeScript
/// </summary>
public class TypeScriptCodeGenerator : BaseCodeGenerator
{
    private readonly TypeScriptTypeMapper _typescriptTypeMapper;
    private readonly TypeScriptCodeRenderer _codeRenderer;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IQueryWrapperRenderer? _queryWrapperRenderer;
    private readonly IModelFormatRenderer? _zodModelRenderer;
    private readonly IEnumFormatRenderer? _zodEnumRenderer;

    public TypeScriptCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<TypeScriptCodeGenerator> logger,
        ILoggerFactory loggerFactory,
        IQueryWrapperRenderer? queryWrapperRenderer = null,
        IModelFormatRenderer? zodModelRenderer = null,
        IEnumFormatRenderer? zodEnumRenderer = null)
        : base(typeMapper, namingConverter, fileWriter, logger, GenerationLanguage.TypeScript)
    {
        _typescriptTypeMapper = typeMapper as TypeScriptTypeMapper ?? throw new InvalidOperationException("TypeMapper must be TypeScriptTypeMapper");
        _codeRenderer = new TypeScriptCodeRenderer();
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _queryWrapperRenderer = queryWrapperRenderer;
        _zodModelRenderer = zodModelRenderer;
        _zodEnumRenderer = zodEnumRenderer;
    }

    public override bool SupportsLanguage(GenerationLanguage language)
    {
        return language == GenerationLanguage.TypeScript;
    }

    public override bool SupportsOptions(GenerationOptions? options)
    {
        // Базовый TypeScript генератор поддерживает базовые опции
        // Специализированные генераторы будут проверять конкретные комбинации
        return true;
    }

    public override async Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config)
    {
        var result = new GenerationResult
        {
            Success = false,
            GeneratedFiles = new Dictionary<string, string>(8192),
            Errors = new List<string>(8192),
            Warnings = new List<string>(8192)
        };

        try
        {
            // Валидация входных данных
            var validationErrors = ValidateInputs(document, config);
            if (validationErrors.Count > 0)
            {
                result.Errors = validationErrors;
                result.Success = false;
                return result;
            }

            // Шаблоны больше не используются - используется TypeScriptCodeRenderer

            Logger.LogInformation("Starting TypeScript code generation");

            // Инициализируем кэши
            InitializeCaches();

            // Предзаполняем кэш операций для быстрого поиска
            BuildOperationCache(document);

            // Извлекаем данные из OpenAPI документа
            var extractedData = ExtractData(document);

            // Валидируем извлеченные данные
            ValidateExtractedData(extractedData, config, result);

            var modelTemplateDataList = extractedData.Models;
            var operationTemplateDataList = extractedData.Operations;
            var enumTemplateDataList = extractedData.Enums;
            var schemaResolver = extractedData.SchemaResolver;

            // Получаем TypeScript опции
            var typescriptOptions = GetTypeScriptOptions(config);

            // Генерируем файлы
            var generatedFiles = new Dictionary<string, string>();
            var generationErrors = new List<string>();
            var generationWarnings = new List<string>();

            // Унифицированная генерация моделей и enum через один processor с несколькими renderers
            if (config.GenerateModels || config.GenerateEnums)
            {
                try
                {
                    var unifiedFiles = await GenerateModelsAndEnumsUnifiedAsync(
                        modelTemplateDataList.Cast<IModelTemplateData>().ToList(),
                        enumTemplateDataList,
                        document,
                        schemaResolver,
                        config,
                        typescriptOptions);

                    foreach (var file in unifiedFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                }
                catch (Exception ex)
                {
                    HandleGenerationError(ex, "models and enums", generationErrors,
                        modelTemplateDataList.Count + enumTemplateDataList.Count);
                }
            }

            if (config.GenerateClients)
            {
                try
                {
                    // Всегда генерируем базовые клиенты (axios или fetch) в отдельные файлы
                    var clientFiles = GenerateClients(operationTemplateDataList, config, typescriptOptions);
                    foreach (var file in clientFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }

                    // Если UseTanStackQuery = true и renderer доступен, генерируем TanStack Query хуки
                    if (typescriptOptions.UseTanStackQuery && _queryWrapperRenderer != null)
                    {
                        try
                        {
                            var queryFiles = await GenerateTanStackQueryHooksAsync(operationTemplateDataList, config, typescriptOptions);
                            foreach (var file in queryFiles)
                            {
                                generatedFiles[file.Key] = file.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to generate TanStack Query hooks");
                            var errors = result.Errors.ToList();
                            errors.Add($"Failed to generate TanStack Query hooks: {ex.Message}");
                            result.Errors = errors;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleGenerationError(ex, "clients", generationErrors, operationTemplateDataList.Count);
                }
            }

            // Добавляем ошибки генерации
            if (generationErrors.Count > 0)
            {
                var errors = result.Errors.ToList();
                errors.AddRange(generationErrors);
                result.Errors = errors;
            }

            // Записываем файлы
            await WriteGeneratedFilesAsync(generatedFiles, result);

            result.Success = result.Errors.Count == 0;
            result.GeneratedFiles = generatedFiles;

            if (result.Success)
            {
                Logger.LogInformation("TypeScript code generation completed successfully. Generated {Count} files", generatedFiles.Count);
            }
            else
            {
                Logger.LogWarning("TypeScript code generation completed with errors. Generated {Count} files, {ErrorCount} errors",
                    generatedFiles.Count, result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error during TypeScript code generation: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" (Inner: {ex.InnerException.Message})";
            }

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                Logger.LogError(ex, "Error during TypeScript code generation. Stack trace: {StackTrace}", ex.StackTrace);
            }
            else
            {
                Logger.LogError(ex, "Error during TypeScript code generation");
            }

            result.Errors = new List<string> { errorMessage };
            result.Success = false;
        }

        return result;
    }


    /// <summary>
    /// Получает TypeScript специфичные опции из конфигурации
    /// </summary>
    private static TypeScriptGenerationOptions GetTypeScriptOptions(GenerationConfiguration config)
    {
        // Пытаемся получить TypeScript опции из config.Options
        if (config.Options?.TypeScriptOptions is TypeScriptGenerationOptions typescriptOptions)
        {
            // Если UseTanStackQuery не задан явно, но QueryWrapperLibrary = TanStackQuery, устанавливаем UseTanStackQuery = true
            if (!typescriptOptions.UseTanStackQuery &&
                config.Options.QueryWrapperLibrary == QueryWrapperLibrary.TanStackQuery)
            {
                typescriptOptions.UseTanStackQuery = true;
            }

            // Если HttpClientLibrary не задан в TypeScript опциях, но задан на верхнем уровне, используем его
            if (typescriptOptions.HttpClientLibrary == HttpClientLibrary.Fetch &&
                config.Options.HttpClientLibrary.HasValue)
            {
                typescriptOptions.HttpClientLibrary = config.Options.HttpClientLibrary.Value;
            }

            // Если ValidationLibrary не задан в TypeScript опциях, но задан на верхнем уровне, используем его
            if (typescriptOptions.ValidationLibrary == null &&
                config.Options.ValidationLibrary.HasValue)
            {
                typescriptOptions.ValidationLibrary = config.Options.ValidationLibrary.Value;
            }

            return typescriptOptions;
        }

        // Если TypeScript опции не заданы, создаем их на основе общих опций
        var defaultOptions = new TypeScriptGenerationOptions();

        if (config.Options != null)
        {
            if (config.Options.HttpClientLibrary.HasValue)
            {
                defaultOptions.HttpClientLibrary = config.Options.HttpClientLibrary.Value;
            }

            if (config.Options.ValidationLibrary.HasValue)
            {
                defaultOptions.ValidationLibrary = config.Options.ValidationLibrary.Value;
            }

            if (config.Options.QueryWrapperLibrary == QueryWrapperLibrary.TanStackQuery)
            {
                defaultOptions.UseTanStackQuery = true;
            }
        }

        return defaultOptions;
    }

    // Методы MapModelTypes и MapOperationTypes удалены - маппинг теперь выполняется в extractors

    /// <summary>
    /// Унифицированная генерация моделей и enum через один processor с несколькими renderers
    /// </summary>
    private async Task<Dictionary<string, string>> GenerateModelsAndEnumsUnifiedAsync(
        IReadOnlyList<IModelTemplateData> modelTemplateDataList,
        IReadOnlyList<Core.Models.TemplateData.EnumTemplateData> enumTemplateDataList,
        OpenApiDocument document,
        ISchemaResolver schemaResolver,
        GenerationConfiguration config,
        TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>(16 * 1024);

        // Создаем словарь enum'ов по имени для быстрой проверки
        var enumsByName = enumTemplateDataList.ToDictionary(
            e => e.Name,
            StringComparer.OrdinalIgnoreCase);

        // Создаем renderers один раз
        // Zod renderers теперь передаются через DI в конструктор (решает циклические зависимости)
        var interfaceModelRenderer = new Renderers.InterfaceModelRenderer();
        var typeScriptEnumRenderer = new Renderers.TypeScriptEnumRenderer();
        var typeScriptUnionTypeRenderer = new Renderers.TypeScriptUnionTypeRenderer();

        // Генерируем модели
        if (config.GenerateModels)
        {
            Logger.LogDebug("Generating models. Count: {Count}", modelTemplateDataList.Count);

            foreach (var modelTemplateData in modelTemplateDataList)
            {
                if (string.IsNullOrWhiteSpace(modelTemplateData.Name))
                {
                    Logger.LogWarning("Skipping model with empty name");
                    continue;
                }

                // Пропускаем модели, которые являются enum'ами
                if (enumsByName.ContainsKey(modelTemplateData.Name))
                {
                    Logger.LogDebug("Skipping model '{ModelName}' - it is an enum", modelTemplateData.Name);
                    continue;
                }

                // Устанавливаем формат модели из опций, если не установлен
                // Для TypeScript: ModelFormat.Interfaces -> Core ModelFormat.Interface, ModelFormat.ValidationSchemas -> Core ModelFormat.ValidationSchema
                if (modelTemplateData.Format == ValIO.Core.Models.TemplateData.ModelFormat.Class)
                {
                    modelTemplateData.Format = options.ModelFormat == TypeScriptModelFormat.ValidationSchemas
                        ? ValIO.Core.Models.TemplateData.ModelFormat.ValidationSchema
                        : ValIO.Core.Models.TemplateData.ModelFormat.Interface;
                }

                // Создаем типизированные Options для TypeScript
                var modelOptions = new TypeScriptModelTemplateDataOptions
                {
                    ValidationLibrary = options.ValidationLibrary ?? ValidationLibrary.Zod
                };

                // Создаем типизированную версию TemplateData
                var typedModelData = new Core.Models.TemplateData.ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions>
                {
                    Name = modelTemplateData.Name,
                    Description = modelTemplateData.Description,
                    Language = modelTemplateData.Language,
                    Format = modelTemplateData.Format,
                    Dependencies = modelTemplateData.Dependencies,
                    BaseClassName = modelTemplateData.BaseClassName,
                    Namespace = modelTemplateData.Namespace,
                    ImportPath = modelTemplateData.ImportPath,
                    Options = modelOptions,
                    Properties = modelTemplateData.Properties.Select(p =>
                    {
                        return new Core.Models.TemplateData.PropertyTemplateData<TypeScriptPropertyTemplateDataOptions>
                        {
                            Name = p.Name,
                            TypeInfo = p.TypeInfo,
                            Description = p.Description,
                            IsRequired = p.IsRequired,
                            IsNullable = p.IsNullable,
                            IsOptional = p.IsOptional,
                            DefaultValue = p.DefaultValue,
                            Min = p.Min,
                            Max = p.Max,
                            MinLength = p.MinLength,
                            MaxLength = p.MaxLength,
                            ExclusiveMin = p.ExclusiveMin,
                            ExclusiveMax = p.ExclusiveMax,
                            Pattern = p.Pattern,
                            Options = new TypeScriptPropertyTemplateDataOptions()
                        };
                    }).ToList(),
                    BaseClassProperties = modelTemplateData.BaseClassProperties.Select(p =>
                    {
                        return new Core.Models.TemplateData.PropertyTemplateData<TypeScriptPropertyTemplateDataOptions>
                        {
                            Name = p.Name,
                            TypeInfo = p.TypeInfo,
                            Description = p.Description,
                            IsRequired = p.IsRequired,
                            IsNullable = p.IsNullable,
                            IsOptional = p.IsOptional,
                            DefaultValue = p.DefaultValue,
                            Min = p.Min,
                            Max = p.Max,
                            MinLength = p.MinLength,
                            MaxLength = p.MaxLength,
                            ExclusiveMin = p.ExclusiveMin,
                            ExclusiveMax = p.ExclusiveMax,
                            Pattern = p.Pattern,
                            Options = new TypeScriptPropertyTemplateDataOptions()
                        };
                    }).ToList(),
                    OwnProperties = modelTemplateData.OwnProperties.Select(p =>
                    {
                        return new Core.Models.TemplateData.PropertyTemplateData<TypeScriptPropertyTemplateDataOptions>
                        {
                            Name = p.Name,
                            TypeInfo = p.TypeInfo,
                            Description = p.Description,
                            IsRequired = p.IsRequired,
                            IsNullable = p.IsNullable,
                            IsOptional = p.IsOptional,
                            DefaultValue = p.DefaultValue,
                            Min = p.Min,
                            Max = p.Max,
                            MinLength = p.MinLength,
                            MaxLength = p.MaxLength,
                            ExclusiveMin = p.ExclusiveMin,
                            ExclusiveMax = p.ExclusiveMax,
                            Pattern = p.Pattern,
                            Options = new TypeScriptPropertyTemplateDataOptions()
                        };
                    }).ToList()
                };

                // Передаем интерфейс IModelTemplateData в renderer (без каста)
                var modelCode = interfaceModelRenderer.Render(typedModelData);
                var modelFileName = $"{NamingConverter.ToCamelCase(modelTemplateData.Name)}.model.ts";
                var modelFilePath = ResolveFilePath(modelFileName, config, FileCategory.Models);

                files[modelFilePath] = modelCode;

                // Генерируем Zod схему, если ValidationLibrary = Zod и renderer доступен
                if (modelOptions.ValidationLibrary == ValidationLibrary.Zod && _zodModelRenderer != null)
                {
                    try
                    {
                        // Устанавливаем формат на ValidationSchema
                        var originalFormat = typedModelData.Format;
                        typedModelData.Format = ValIO.Core.Models.TemplateData.ModelFormat.ValidationSchema;

                        // Передаем интерфейс IModelTemplateData в renderer (без каста)
                        var schemaCode = _zodModelRenderer.Render(typedModelData);
                        var schemaFileName = $"{NamingConverter.ToCamelCase(typedModelData.Name)}.schema.ts";
                        var schemaFilePath = ResolveFilePath(schemaFileName, config, FileCategory.Models);

                        files[schemaFilePath] = schemaCode;

                        // Восстанавливаем оригинальный формат
                        typedModelData.Format = originalFormat;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to generate Zod schema for model: {ModelName}", modelTemplateData.Name);
                        // Продолжаем генерацию других моделей
                    }
                }
            }
        }

        // Генерируем enum'ы
        if (config.GenerateEnums)
        {
            Logger.LogDebug("Generating enums. Count: {Count}", enumTemplateDataList.Count);

            foreach (var enumTemplateData in enumTemplateDataList)
            {
                if (string.IsNullOrWhiteSpace(enumTemplateData.Name))
                {
                    Logger.LogWarning("Skipping enum with empty name");
                    continue;
                }

                // Устанавливаем формат enum из опций, если не установлен
                // Для TypeScript: EnumFormat.Enum -> Core EnumFormat.Enum, EnumFormat.UnionType -> Core EnumFormat.Union
                if (enumTemplateData.Format == ValIO.Core.Models.TemplateData.EnumFormat.Enum && options.EnumFormat != TypeScriptEnumFormat.Enum)
                {
                    enumTemplateData.Format = options.EnumFormat == TypeScriptEnumFormat.UnionType
                        ? ValIO.Core.Models.TemplateData.EnumFormat.Union
                        : ValIO.Core.Models.TemplateData.EnumFormat.Enum;
                }

                // Создаем типизированные Options для TypeScript
                var enumOptions = new TypeScriptEnumTemplateDataOptions
                {
                    ValidationLibrary = options.ValidationLibrary ?? ValidationLibrary.Zod
                };

                // Создаем типизированную версию TemplateData
                var typedEnumData = new Core.Models.TemplateData.EnumTemplateData<TypeScriptEnumTemplateDataOptions>
                {
                    Name = enumTemplateData.Name,
                    Description = enumTemplateData.Description,
                    Language = enumTemplateData.Language,
                    Format = enumTemplateData.Format,
                    UnderlyingType = enumTemplateData.UnderlyingType,
                    Namespace = enumTemplateData.Namespace,
                    ImportPath = enumTemplateData.ImportPath,
                    Options = enumOptions,
                    Values = enumTemplateData.Values
                };

                // Приводим к базовому типу для renderer
                var enumTemplateDataForRenderer = (Core.Models.TemplateData.EnumTemplateData)(object)typedEnumData;

                // Генерируем TypeScript enum/union type
                IEnumFormatRenderer enumRenderer = enumTemplateData.Format switch
                {
                    ValIO.Core.Models.TemplateData.EnumFormat.Union => typeScriptUnionTypeRenderer,
                    _ => typeScriptEnumRenderer
                };

                var enumCode = enumRenderer.Render(enumTemplateData);
                var enumFileName = $"{NamingConverter.ToCamelCase(typedEnumData.Name)}.enum.ts";
                var enumFilePath = ResolveFilePath(enumFileName, config, FileCategory.Models);

                files[enumFilePath] = enumCode;

                // Генерируем Zod enum схему, если ValidationLibrary = Zod и renderer доступен
                if (enumOptions.ValidationLibrary == ValidationLibrary.Zod && _zodEnumRenderer != null)
                {
                    try
                    {
                        // Устанавливаем формат на ValidationSchema
                        var originalFormat = typedEnumData.Format;
                        typedEnumData.Format = ValIO.Core.Models.TemplateData.EnumFormat.ValidationSchema;

                        // Приводим к базовому типу для renderer
                        var schemaEnumDataForRenderer = (Core.Models.TemplateData.EnumTemplateData)(object)typedEnumData;

                        // Рендерим Zod enum схему через интерфейс
                        var schemaCode = _zodEnumRenderer.Render(schemaEnumDataForRenderer);
                        var schemaFileName = $"{NamingConverter.ToCamelCase(typedEnumData.Name)}.schema.ts";
                        var schemaFilePath = ResolveFilePath(schemaFileName, config, FileCategory.Models);

                        files[schemaFilePath] = schemaCode;

                        // Восстанавливаем оригинальный формат
                        typedEnumData.Format = originalFormat;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to generate Zod schema for enum: {EnumName}", typedEnumData.Name);
                        // Продолжаем генерацию других enum'ов
                    }
                }
            }
        }

        return files;
    }

    /// <summary>
    /// Переопределяет BuildOperationCache для более сложной логики с operationId
    /// </summary>
    protected override void BuildOperationCache(OpenApiDocument document)
    {
        OperationCache = new Dictionary<string, OpenApiOperation>(16 * 1024);

        if (document.Paths == null)
            return;

        foreach (var pathEntry in document.Paths)
        {
            if (pathEntry.Value.Operations == null)
                continue;

            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var openApiOperation = operationEntry.Value;
                var operationId = openApiOperation.OperationId ??
                                  $"{operationEntry.Key.Method.ToLower()}{pathEntry.Key.Replace("/", "").Replace("{", "").Replace("}", "")}";

                var cacheKey = $"{operationId}|{operationEntry.Key.Method}|{pathEntry.Key}";
                OperationCache[cacheKey] = openApiOperation;
            }
        }

        Logger.LogDebug("Built operation cache with {Count} entries", OperationCache.Count);
    }


    private string GenerateOperationId(string path, HttpMethod httpMethod)
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"{httpMethod.Method}Request";

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var lastSegment = segments.Length > 0 ? segments[^1] : "request";
        lastSegment = lastSegment.Replace("{", "").Replace("}", "");

        var methodPrefix = httpMethod.Method.ToUpperInvariant();
        return $"{methodPrefix}{NamingConverter.ToPascalCase(lastSegment)}";
    }

    /// <summary>
    /// Определяет путь к файлу на основе конфигурации организации файлов
    /// </summary>
    // Метод ResolveFilePath и enum FileCategory теперь в BaseCodeGenerator
    // Методы DecodeHtmlEntities и FormatEnumValue удалены - они больше не используются
    // Декодирование HTML и форматирование enum значений теперь выполняется в renderers

    /// <summary>
    /// Генерирует код модели для query параметров
    /// </summary>
    private string GenerateQueryParametersModelCode(string interfaceName, List<Core.Models.TemplateData.ParameterTemplateData> parameters, GenerationConfiguration config,
        TypeScriptGenerationOptions options)
    {
        var properties = parameters.Select(param =>
        {
            return new Core.Models.TemplateData.PropertyTemplateData<TypeScriptPropertyTemplateDataOptions>
            {
                Name = param.VariableName, // Используем VariableName (camelCase)
                TypeInfo = param.TypeInfo,
                IsOptional = !param.IsRequired,
                Description = param.Description,
                Options = new TypeScriptPropertyTemplateDataOptions()
            };
        }).ToList();

        // Собираем используемые типы для импортов
        var importedTypes = properties.SelectMany(p => p.TypeInfo.Dependencies).Distinct().ToList();

        var modelOptions = new TypeScriptModelTemplateDataOptions
        {
            ValidationLibrary = options.ValidationLibrary ?? ValidationLibrary.Zod
        };

        var templateData = new Core.Models.TemplateData.ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions>
        {
            Name = interfaceName, // Используем переданное имя (уже в PascalCase)
            Description = null, // Query parameters модели обычно не имеют описания
            Properties = properties,
            Language = GenerationLanguage.TypeScript,
            Format = ValIO.Core.Models.TemplateData.ModelFormat.Interface,
            Dependencies = importedTypes,
            Options = modelOptions
        };

        // Передаем интерфейс IModelTemplateData в renderer (без каста)
        var modelRenderer = new Renderers.InterfaceModelRenderer();
        return modelRenderer.Render(templateData);
    }

    // Методы GenerateModelCode, CreateApiModelFromQueryParameters, CollectModelUsedTypes и связанные вспомогательные методы удалены
    // - они больше не используются, так как данные теперь извлекаются через extractors в виде TemplateData

    private Dictionary<string, string> GenerateClients(IReadOnlyList<Core.Models.TemplateData.OperationTemplateData> operationTemplateDataList, GenerationConfiguration config,
        TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>();

        var groupedOperations = GroupOperations(operationTemplateDataList, config);

        // Собираем модели query parameters для генерации
        var queryParametersModels = new Dictionary<string, List<Core.Models.TemplateData.ParameterTemplateData>>();
        // TypeScript всегда использует отдельные параметры для query, не модель
        var useQueryParametersModel = false;

        foreach (var operationTemplate in operationTemplateDataList)
        {
            if (useQueryParametersModel && operationTemplate.QueryParameters.Count > 0 &&
                !string.IsNullOrEmpty(operationTemplate.QueryParametersModelTypeName))
            {
                if (!queryParametersModels.ContainsKey(operationTemplate.QueryParametersModelTypeName))
                {
                    queryParametersModels[operationTemplate.QueryParametersModelTypeName] =
                        operationTemplate.QueryParameters.ToList();
                }
            }
        }

        // Генерируем модели query parameters
        foreach (var queryModel in queryParametersModels)
        {
            var modelCode = GenerateQueryParametersModelCode(queryModel.Key, queryModel.Value, config, options);
            var fileName = $"{NamingConverter.ToCamelCase(queryModel.Key)}.model.ts";
            var filePath = ResolveFilePath(fileName, config, FileCategory.Models);

            files[filePath] = modelCode;
        }

        // Генерация Zod схем для query parameters моделей теперь выполняется через ModelFormatRenderer

        // Генерируем клиенты
        foreach (var group in groupedOperations)
        {
            var clientCode = GenerateClientCode(group.Value, group.Key, config, options);

            // Определяем расширение файла в зависимости от HTTP клиента
            var fileExtension = options.HttpClientLibrary == HttpClientLibrary.Axios ? ".axios.ts" : ".fetch.ts";
            var fileName = $"{NamingConverter.ToCamelCase(group.Key)}{fileExtension}";
            var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);

            files[filePath] = clientCode;
        }

        return files;
    }

    protected override Dictionary<string, List<Core.Models.TemplateData.OperationTemplateData>> GroupOperations(IReadOnlyList<Core.Models.TemplateData.OperationTemplateData> operations,
        GenerationConfiguration config)
    {
        var groups = new Dictionary<string, List<Core.Models.TemplateData.OperationTemplateData>>();

        switch (config.FileOrganization.GroupingStrategy)
        {
            case FileGroupingStrategy.ByTag:
                foreach (var operation in operations)
                {
                    var tag = operation.Tags?.FirstOrDefault() ?? "Default";
                    var sanitizedTag = NamingConverter.SanitizeIdentifier(tag);
                    if (!groups.ContainsKey(sanitizedTag))
                    {
                        groups[sanitizedTag] = new List<Core.Models.TemplateData.OperationTemplateData>();
                    }

                    groups[sanitizedTag].Add(operation);
                }

                break;

            case FileGroupingStrategy.ByRoute:
                foreach (var operation in operations)
                {
                    var routeGroup = ExtractRouteGroup(operation.Path);
                    if (!groups.ContainsKey(routeGroup))
                    {
                        groups[routeGroup] = new List<Core.Models.TemplateData.OperationTemplateData>();
                    }

                    groups[routeGroup].Add(operation);
                }

                break;

            case FileGroupingStrategy.SingleFile:
            default:
                groups["Api"] = operations.ToList();
                break;
        }

        return groups;
    }

    protected override string ExtractRouteGroup(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Default";

        var cleanPath = path.Trim('/');
        var segments = cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return "Default";

        var lastSegment = segments[^1];
        if (lastSegment.StartsWith("{") && segments.Length > 1)
        {
            lastSegment = segments[^2];
        }

        var sanitized = NamingConverter.SanitizeIdentifier(lastSegment);
        return NamingConverter.ToPascalCase(sanitized);
    }

    private string GenerateClientCode(List<Core.Models.TemplateData.OperationTemplateData> operations, string clientName, GenerationConfiguration config, TypeScriptGenerationOptions options)
    {
        // Собираем используемые типы для импортов
        var importedTypes = operations.SelectMany(op => op.Dependencies).Distinct().ToList();

        // Вычисляем путь к моделям для импортов
        string? modelsImportPath = null;
        if (config.FileOrganization.SeparateModels)
        {
            var modelsPath = config.FileOrganization.ModelsPath ?? "models";
            if (config.FileOrganization.SeparateClients)
            {
                // Клиенты в отдельной папке (api/), модели в отдельной папке (models/)
                // Путь из api/ к models/ = ../models/
                modelsImportPath = $"../{modelsPath}";
            }
            else
            {
                // Клиенты в корне, модели в отдельной папке (models/)
                // Путь из корня к models/ = ./models/
                modelsImportPath = $"./{modelsPath}";
            }
        }

        // Создаем типизированные Options для Client
        var clientOptions = new TypeScriptClientTemplateDataOptions
        {
            AxiosOptions = options.Axios
        };

        // Создаем типизированные Operations с Options
        var typedOperations = operations.Select(op =>
        {
            var operationOptions = new TypeScriptOperationTemplateDataOptions();
            return new Core.Models.TemplateData.OperationTemplateData<TypeScriptOperationTemplateDataOptions>
            {
                MethodName = op.MethodName,
                HttpMethod = op.HttpMethod,
                Path = op.Path,
                Summary = op.Summary,
                Description = op.Description,
                ReturnTypeInfo = op.ReturnTypeInfo,
                HasRequestBody = op.HasRequestBody,
                RequestBodyParameter = op.RequestBodyParameter,
                RequestBodyTypeInfo = op.RequestBodyTypeInfo,
                RequestBodyContentType = op.RequestBodyContentType,
                HasResponseType = op.HasResponseType,
                PathParameters = op.PathParameters,
                QueryParameters = op.QueryParameters,
                HeaderParameters = op.HeaderParameters,
                IsFormData = op.IsFormData,
                FormDataProperties = op.FormDataProperties,
                Responses = op.Responses,
                Language = op.Language,
                Dependencies = op.Dependencies,
                UseQueryParametersModel = op.UseQueryParametersModel,
                QueryParametersModelTypeName = op.QueryParametersModelTypeName,
                QueryParametersModelImportPath = op.QueryParametersModelImportPath,
                RequestSchemaName = op.RequestSchemaName,
                RequestSchemaImportPath = op.RequestSchemaImportPath,
                ResponseSchemaName = op.ResponseSchemaName,
                ResponseSchemaImportPath = op.ResponseSchemaImportPath,
                Tags = op.Tags,
                Options = operationOptions
            };
        }).ToList();

        var templateData = new Core.Models.TemplateData.ClientTemplateData<TypeScriptClientTemplateDataOptions, TypeScriptOperationTemplateDataOptions>
        {
            Name = clientName,
            Operations = typedOperations,
            Language = GenerationLanguage.TypeScript,
            HttpClientLibrary = options.HttpClientLibrary,
            Dependencies = importedTypes,
            ModelsPath = modelsImportPath,
            Options = clientOptions
        };

        // Передаем интерфейс IClientTemplateData в renderer (без каста)
        return _codeRenderer.RenderClient(templateData);
    }

    /// <summary>
    /// Собирает все используемые типы из операций для генерации импортов
    /// </summary>
    // Методы CollectUsedTypes, CollectTypesFromTypeInfo, CreateTypeScriptOperationTemplateData, DetermineReturnTypeInfo, FindSchemaInContent удалены
    // - они больше не используются, так как операции теперь извлекаются через extractors в виде OperationTemplateData

    // Методы ValidateModelTemplateData и ValidateOperationTemplateData теперь в BaseCodeGenerator

    /// <summary>
    /// Генерирует TanStack Query хуки для операций
    /// </summary>
    private async Task<Dictionary<string, string>> GenerateTanStackQueryHooksAsync(
        IReadOnlyList<Core.Models.TemplateData.OperationTemplateData> operationTemplateDataList,
        GenerationConfiguration config,
        TypeScriptGenerationOptions options)
    {
        if (_queryWrapperRenderer == null)
            return new Dictionary<string, string>();

        var files = new Dictionary<string, string>();

        // Группируем операции по стратегии из конфигурации
        var groupedOperations = GroupOperations(operationTemplateDataList, config);

        foreach (var group in groupedOperations)
        {
            var groupName = group.Key;
            var operations = group.Value;

            var hooksCode = GenerateTanStackQueryHooksCode(operations, groupName, config, options);
            var fileName = $"{NamingConverter.ToCamelCase(groupName)}.hooks.ts";
            var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);

            files[filePath] = hooksCode;
        }

        return files;
    }

    /// <summary>
    /// Генерирует код TanStack Query хуков для группы операций
    /// </summary>
    private string GenerateTanStackQueryHooksCode(
        List<Core.Models.TemplateData.OperationTemplateData> operations,
        string groupName,
        GenerationConfiguration config,
        TypeScriptGenerationOptions options)
    {
        if (_queryWrapperRenderer == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Импорты для TanStack Query
        sb.AppendLine("import { useQuery, useMutation, useInfiniteQuery, useQueryClient } from '@tanstack/react-query';");
        sb.AppendLine();

        // Генерируем хуки для каждой операции
        foreach (var operation in operations)
        {
            var isQuery = operation.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase);

            if (isQuery)
            {
                var hookCode = _queryWrapperRenderer.RenderQueryHook(operation);
                sb.AppendLine(hookCode);
                sb.AppendLine();
            }
            else
            {
                var hookCode = _queryWrapperRenderer.RenderMutationHook(operation);
                sb.AppendLine(hookCode);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // Метод EnsureTemplatesPreloadedAsync удален - шаблоны больше не используются
}