using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.CSharp.Models;
using System.Linq;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Генератор кода для C#
/// </summary>
public class CSharpCodeGenerator : ICodeGenerator
{
    private readonly ITypeMapper _typeMapper;
    private readonly CSharpTypeMapper _csharpTypeMapper;
    private readonly INamingConverter _namingConverter;
    private readonly IFileWriter _fileWriter;
    private readonly ITemplateRegistry _templateRegistry;
    private readonly ILogger<CSharpCodeGenerator> _logger;
    private bool _templatesPreloaded;
    
    // Кэш для поиска операций (path + method -> operation)
    private Dictionary<string, OpenApiOperation>? _operationCache;
    
    // Кэш для маппинга схем в TypeInfo (schema reference -> TypeInfo)
    private Dictionary<string, TypeInfo>? _schemaTypeInfoCache;

    public CSharpCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ITemplateRegistry templateRegistry,
        ILogger<CSharpCodeGenerator> logger)
    {
        _typeMapper = typeMapper;
        _csharpTypeMapper = typeMapper as CSharpTypeMapper ?? throw new InvalidOperationException("TypeMapper must be CSharpTypeMapper");
        _namingConverter = namingConverter;
        _fileWriter = fileWriter;
        _templateRegistry = templateRegistry ?? throw new ArgumentNullException(nameof(templateRegistry));
        _logger = logger;
        _templatesPreloaded = false;
    }

    public bool SupportsLanguage(GenerationLanguage language)
    {
        return language == GenerationLanguage.CSharp;
    }

    public bool SupportsOptions(GenerationOptions? options)
    {
        // C# генератор поддерживает любые опции (или их отсутствие)
        return true;
    }

    /// <summary>
    /// Валидирует входные данные для генерации
    /// </summary>
    private List<string> ValidateInputs(OpenApiDocument? document, GenerationConfiguration? config)
    {
        var errors = new List<string>();

        if (document == null)
        {
            errors.Add("OpenAPI document cannot be null");
        }

        if (config == null)
        {
            errors.Add("Generation configuration cannot be null");
            return errors; // Нет смысла продолжать валидацию без конфигурации
        }

        if (string.IsNullOrWhiteSpace(config.OutputPath))
        {
            errors.Add("OutputPath is required in configuration");
        }

        if (config.Language != GenerationLanguage.CSharp)
        {
            errors.Add($"Language must be CSharp, but got {config.Language}");
        }

        return errors;
    }

    public async Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config)
    {
        var result = new GenerationResult
        {
            Success = false,
            GeneratedFiles = new Dictionary<string, string>(),
            Errors = new List<string>(),
            Warnings = new List<string>()
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

            // Предзагружаем шаблоны при первом использовании
            if (!_templatesPreloaded)
            {
                await EnsureTemplatesPreloadedAsync();
            }

            _logger.LogInformation("Starting C# code generation");

            // Инициализируем кэши
            _operationCache = new Dictionary<string, OpenApiOperation>();
            _schemaTypeInfoCache = new Dictionary<string, TypeInfo>();
            
            // Предзаполняем кэш операций для быстрого поиска
            BuildOperationCache(document);

            // Создаем SchemaResolver для документа
            var schemaResolver = new SchemaResolver(document);

            // Создаем extractors с SchemaResolver
            var modelExtractor = new ModelExtractor(schemaResolver, _namingConverter);
            var operationExtractor = new OperationExtractor(schemaResolver);
            var enumExtractor = new EnumExtractor(schemaResolver, _namingConverter);

            // Извлекаем модели
            var models = modelExtractor.ExtractModels(document);
            _logger.LogInformation("Extracted {Count} models", models.Count);
            
            if (config.GenerateModels && models.Count == 0)
            {
                result.Warnings = new List<string> { "No models found in OpenAPI schema" };
            }
            
            // Валидируем модели на пустые имена
            ValidateModels(models, result);

            // Извлекаем операции
            var operations = operationExtractor.ExtractOperations(document);
            _logger.LogInformation("Extracted {Count} operations", operations.Count);
            
            if (config.GenerateClients && operations.Count == 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.Add("No operations found in OpenAPI schema");
                result.Warnings = warnings;
            }
            
            // Валидируем операции на дублирующиеся operationId
            ValidateOperations(operations, result);

            // Маппим типы для моделей
            var mappingErrors = new List<string>();
            var mappingWarnings = new List<string>();
            foreach (var model in models)
            {
                try
                {
                    MapModelTypes(model, schemaResolver);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to map types for model '{model.Name}': {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMsg += $" (Inner: {ex.InnerException.Message})";
                    }
                    mappingErrors.Add(errorMsg);
                    _logger.LogWarning(ex, "Failed to map types for model: {ModelName}", model.Name);
                }
            }

            // Маппим типы для операций
            foreach (var operation in operations)
            {
                try
                {
                    MapOperationTypes(operation, document, schemaResolver);
                }
                catch (Exception ex)
                {
                    var warningMsg = $"Failed to map types for operation '{operation.OperationId}' (Path: {operation.Path}, Method: {operation.HttpMethod.Method}): {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        warningMsg += $" (Inner: {ex.InnerException.Message})";
                    }
                    mappingWarnings.Add(warningMsg);
                    _logger.LogWarning(ex, "Failed to map types for operation: {OperationId} at {Path} {Method}", 
                        operation.OperationId, operation.Path, operation.HttpMethod.Method);
                }
            }

            // Извлекаем enum'ы
            var enums = enumExtractor.ExtractEnums(document);
            _logger.LogInformation("Extracted {Count} enums", enums.Count);
            
            if (config.GenerateEnums && enums.Count == 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.Add("No enums found in OpenAPI schema");
                result.Warnings = warnings;
            }

            // Добавляем ошибки и предупреждения маппинга
            if (mappingErrors.Count > 0)
            {
                var errors = result.Errors.ToList();
                errors.AddRange(mappingErrors);
                result.Errors = errors;
            }
            
            if (mappingWarnings.Count > 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.AddRange(mappingWarnings);
                result.Warnings = warnings;
            }

            // Если есть критические ошибки, прекращаем генерацию
            if (mappingErrors.Count > 0)
            {
                result.Success = false;
                return result;
            }

            // Генерируем файлы
            var generatedFiles = new Dictionary<string, string>();
            var generationErrors = new List<string>();
            var generationWarnings = new List<string>();

            if (config.GenerateEnums)
            {
                try
                {
                    var enumFiles = GenerateEnums(enums, config);
                    foreach (var file in enumFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to generate enums: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMsg += $" (Inner: {ex.InnerException.Message})";
                    }
                    generationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to generate enums. Count: {Count}", enums.Count);
                }
            }

            if (config.GenerateModels)
            {
                try
                {
                    var modelFiles = GenerateModels(models, config, document, schemaResolver);
                    foreach (var file in modelFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to generate models: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMsg += $" (Inner: {ex.InnerException.Message})";
                    }
                    generationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to generate models. Count: {Count}", models.Count);
                }
            }

            if (config.GenerateClients)
            {
                try
                {
                    var clientFiles = GenerateClients(operations, document, config, schemaResolver);
                    foreach (var file in clientFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to generate clients: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMsg += $" (Inner: {ex.InnerException.Message})";
                    }
                    generationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to generate clients. Operations count: {Count}", operations.Count);
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
            if (generatedFiles.Count > 0)
            {
                try
                {
                    await _fileWriter.WriteFilesAsync(generatedFiles);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to write generated files: {ex.Message}";
                    var errors = result.Errors.ToList();
                    errors.Add(errorMsg);
                    result.Errors = errors;
                    _logger.LogError(ex, "Failed to write generated files");
                }
            }
            else
            {
                var warnings = result.Warnings.ToList();
                warnings.Add("No files were generated");
                result.Warnings = warnings;
            }

            result.Success = result.Errors.Count == 0;
            result.GeneratedFiles = generatedFiles;
            
            if (result.Success)
            {
                _logger.LogInformation("C# code generation completed successfully. Generated {Count} files", generatedFiles.Count);
            }
            else
            {
                _logger.LogWarning("C# code generation completed with errors. Generated {Count} files, {ErrorCount} errors", 
                    generatedFiles.Count, result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error during C# code generation: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" (Inner: {ex.InnerException.Message})";
            }
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                _logger.LogError(ex, "Error during C# code generation. Stack trace: {StackTrace}", ex.StackTrace);
            }
            else
            {
                _logger.LogError(ex, "Error during C# code generation");
            }
            result.Errors = new List<string> { errorMessage };
            result.Success = false;
        }

        return result;
    }

    private void MapModelTypes(ApiModel model, ISchemaResolver schemaResolver)
    {
        if (model.OriginalSchema == null)
            return;

        foreach (var property in model.Properties)
        {
            // Находим схему свойства в оригинальной схеме
            if (model.OriginalSchema.Properties != null &&
                model.OriginalSchema.Properties.TryGetValue(property.Name, out var propertySchema))
            {
                property.Type = _typeMapper.MapPropertyType(propertySchema, property.IsRequired, schemaResolver);
            }
        }
    }

    private void MapOperationTypes(ApiOperation operation, OpenApiDocument document, ISchemaResolver schemaResolver)
    {
        // Находим оригинальную операцию в документе
        var originalOperation = FindOriginalOperation(document, operation);
        if (originalOperation == null)
            return;

        // Маппим типы параметров
        if (originalOperation.Parameters != null)
        {
            var parameterMap = originalOperation.Parameters
                .Where(p => p.Name != null)
                .ToDictionary(p => p.Name!, p => p);

            foreach (var parameter in operation.Parameters)
            {
                if (parameterMap.TryGetValue(parameter.Name, out var originalParam) && originalParam.Schema != null)
                {
                    var resolvedSchema = schemaResolver.ResolveSchema(originalParam.Schema);
                    parameter.Type = _typeMapper.MapPropertyType(resolvedSchema, parameter.IsRequired, schemaResolver);
                }
                else if (string.IsNullOrEmpty(parameter.Type) || parameter.Type == "object")
                {
                    // Если схема не найдена, используем string для параметров по умолчанию
                    parameter.Type = "string";
                }
            }
        }

        // Маппим тип request body
        if (operation.RequestBody != null && originalOperation.RequestBody != null)
        {
            var content = originalOperation.RequestBody.Content?.FirstOrDefault();
            var schema = content.HasValue ? content.Value.Value?.Schema : null;
            if (schema != null)
            {
                var resolvedSchema = schemaResolver.ResolveSchema(schema);
                operation.RequestBody.Type = _typeMapper.MapType(resolvedSchema, schemaResolver);
            }
        }

        // Маппим типы ответов
        if (originalOperation.Responses != null)
        {
            foreach (var responseEntry in originalOperation.Responses)
            {
                if (operation.Responses.TryGetValue(responseEntry.Key, out var apiResponse))
                {
                    var originalResponse = responseEntry.Value;
                    var content = originalResponse.Content?.FirstOrDefault();
                    var schema = content.HasValue ? content.Value.Value?.Schema : null;
                    if (schema != null)
                    {
                        var resolvedSchema = schemaResolver.ResolveSchema(schema);
                        apiResponse.Type = _typeMapper.MapType(resolvedSchema, schemaResolver);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Предзаполняет кэш операций для быстрого поиска
    /// </summary>
    private void BuildOperationCache(OpenApiDocument document)
    {
        if (document.Paths == null || _operationCache == null)
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

                // Создаем ключ: operationId + method + path
                var cacheKey = $"{operationId}|{operationEntry.Key.Method}|{pathEntry.Key}";
                _operationCache[cacheKey] = openApiOperation;
            }
        }
        
        _logger.LogDebug("Built operation cache with {Count} entries", _operationCache.Count);
    }

    /// <summary>
    /// Находит оригинальную операцию в документе (использует кэш для производительности)
    /// </summary>
    private OpenApiOperation? FindOriginalOperation(OpenApiDocument document, ApiOperation operation)
    {
        // Пытаемся найти в кэше
        if (_operationCache != null)
        {
            // Пробуем разные варианты ключа
            var operationId = operation.OperationId ?? GenerateOperationId(operation.Path, operation.HttpMethod);
            var cacheKey = $"{operationId}|{operation.HttpMethod.Method}|{operation.Path}";
            
            if (_operationCache.TryGetValue(cacheKey, out var cachedOperation))
            {
                return cachedOperation;
            }
            
            // Если не нашли по полному ключу, ищем по operationId и method
            var partialKey = $"{operationId}|{operation.HttpMethod.Method}";
            var matches = _operationCache.Where(kvp => kvp.Key.StartsWith(partialKey)).ToList();
            if (matches.Count == 1)
            {
                return matches[0].Value;
            }
        }

        // Fallback: поиск без кэша (для совместимости)
        if (document.Paths == null)
            return null;

        foreach (var pathEntry in document.Paths)
        {
            if (pathEntry.Value.Operations == null)
                continue;

            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var openApiOperation = operationEntry.Value;
                var openApiOperationId = openApiOperation.OperationId ?? 
                    $"{operationEntry.Key.Method.ToLower()}{pathEntry.Key.Replace("/", "").Replace("{", "").Replace("}", "")}";

                if (openApiOperationId == operation.OperationId && 
                    operationEntry.Key.Method == operation.HttpMethod.Method)
                {
                    return openApiOperation;
                }
            }
        }

        return null;
    }

    private Dictionary<string, string> GenerateEnums(IReadOnlyList<ApiEnum> enums, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        foreach (var apiEnum in enums)
        {
            // Валидируем имя enum
            if (string.IsNullOrWhiteSpace(apiEnum.Name))
            {
                _logger.LogWarning("Skipping enum with empty name");
                continue;
            }
            
            var enumCode = GenerateEnumCode(apiEnum, config);
            var fileName = $"{_namingConverter.ToPascalCase(apiEnum.Name)}.cs";
            
            // Определяем путь для файла enum
            // Enum'ы обычно размещаются вместе с моделями или в отдельной папке
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "Models";
                filePath = Path.Combine(config.OutputPath, modelsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = enumCode;
        }

        return files;
    }

    private string GenerateEnumCode(ApiEnum apiEnum, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var enumName = _namingConverter.ToPascalCase(apiEnum.Name);

        var enumValues = apiEnum.Values.Select(v => new EnumValueTemplateData
        {
            Name = _namingConverter.ToPascalCase(v.Name),
            Value = v.Value,
            Description = DecodeHtmlEntities(v.Description),
            ValueString = FormatEnumValue(v.Value, apiEnum.UnderlyingType)
        }).ToList();

        var templateData = new EnumTemplateData
        {
            Namespace = namespaceName,
            EnumName = enumName,
            Description = DecodeHtmlEntities(apiEnum.Description),
            UnderlyingType = apiEnum.UnderlyingType,
            Values = enumValues
        };

        return _templateRegistry.Render("csharp-enum.cshtml", templateData);
    }

    /// <summary>
    /// Декодирует HTML-сущности в тексте
    /// </summary>
    private static string? DecodeHtmlEntities(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Декодируем HTML-сущности (например, &#x418; -> И)
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static string FormatEnumValue(object? value, string underlyingType)
    {
        if (value == null)
            return "0";

        return underlyingType switch
        {
            "string" => $"\"{value}\"",
            "int" or "long" or "short" or "byte" => value.ToString() ?? "0",
            "float" => $"{value}f",
            "double" => value.ToString() ?? "0.0",
            "bool" => value.ToString()?.ToLowerInvariant() ?? "false",
            _ => value.ToString() ?? "0"
        };
    }

    private Dictionary<string, string> GenerateModels(IReadOnlyList<ApiModel> models, GenerationConfiguration config, OpenApiDocument document, ISchemaResolver schemaResolver)
    {
        var files = new Dictionary<string, string>();

        foreach (var model in models)
        {
            // Валидируем имя модели
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                _logger.LogWarning("Skipping model with empty name");
                continue;
            }
            
            var modelCode = GenerateModelCode(model, config, document, schemaResolver);
            var fileName = $"{_namingConverter.ToPascalCase(model.Name)}.cs";
            
            // Определяем путь для файла модели
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "Models";
                filePath = Path.Combine(config.OutputPath, modelsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = modelCode;
        }

        return files;
    }

    private string GenerateModelCode(ApiModel model, GenerationConfiguration config, OpenApiDocument document, ISchemaResolver schemaResolver)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = _namingConverter.ToPascalCase(model.Name);

        // Получаем C# специфичные опции из конфигурации
        var csharpOptions = GetCSharpOptions(config);

        var properties = new List<PropertyTemplateData>();
        foreach (var p in model.Properties)
        {
            // Получаем TypeInfo для свойства
            TypeInfo typeInfo = TypeInfo.Simple("object"); // Значение по умолчанию
            try
            {
                if (model.OriginalSchema?.Properties != null &&
                    model.OriginalSchema.Properties.TryGetValue(p.Name, out var propertySchema))
                {
                    typeInfo = _csharpTypeMapper.MapPropertyTypeInfo(propertySchema, p.IsRequired, schemaResolver);
                }
                else
                {
                    // Пытаемся найти схему в allOf
                    if (model.OriginalSchema?.AllOf != null)
                    {
                        var found = false;
                        foreach (var subSchema in model.OriginalSchema.AllOf)
                        {
                            var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                            if (resolvedSubSchema.Properties != null &&
                                resolvedSubSchema.Properties.TryGetValue(p.Name, out var subPropertySchema))
                            {
                                typeInfo = _csharpTypeMapper.MapPropertyTypeInfo(subPropertySchema, p.IsRequired, schemaResolver);
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            // Если схема не найдена, создаем простой тип из строки
                            typeInfo = TypeInfo.Simple(p.Type);
                        }
                    }
                    else
                    {
                        // Если схема не найдена, создаем простой тип из строки
                        typeInfo = TypeInfo.Simple(p.Type);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map type for property '{PropertyName}' in model '{ModelName}', using 'object' as fallback", 
                    p.Name, model.Name);
                typeInfo = TypeInfo.Simple("object");
            }
            
            properties.Add(new PropertyTemplateData
            {
                Name = _namingConverter.ToPascalCase(p.Name),
                TypeInfo = typeInfo,
                Description = DecodeHtmlEntities(p.Description),
                AccessorType = csharpOptions.PropertyAccessor,
                IsRequired = csharpOptions.UseRequired && p.IsRequired,
                IsNullable = csharpOptions.UseNullable && p.IsNullable,
                DefaultValue = p.DefaultValue,
                Min = p.Min,
                Max = p.Max,
                ExclusiveMin = p.ExclusiveMin,
                ExclusiveMax = p.ExclusiveMax,
                MinLength = p.MinLength,
                MaxLength = p.MaxLength,
                Pattern = p.Pattern
            });
        }

        // Определяем, нужны ли using директивы
        bool needsDataAnnotations = csharpOptions.GenerateValidationAttributes && properties.Any(p => 
            p.IsRequired || p.MinLength.HasValue || p.MaxLength.HasValue || 
            p.Min.HasValue || p.Max.HasValue || !string.IsNullOrEmpty(p.Pattern));
        bool needsRegularExpressions = properties.Any(p => !string.IsNullOrEmpty(p.Pattern));

        var templateData = new ModelTemplateData
        {
            Namespace = namespaceName,
            ClassName = className,
            Description = DecodeHtmlEntities(model.Description),
            Properties = properties,
            ModelType = csharpOptions.ModelType,
            IsSealed = csharpOptions.UseSealed,
            GenerateEqualityMembers = csharpOptions.GenerateEqualityMembers,
            GenerateValidationConstants = csharpOptions.GenerateValidationConstants,
            GenerateValidationAttributes = csharpOptions.GenerateValidationAttributes,
            NeedsDataAnnotations = needsDataAnnotations,
            NeedsRegularExpressions = needsRegularExpressions,
            BaseClassName = model.BaseClassName != null ? _namingConverter.ToPascalCase(model.BaseClassName) : null
        };

        return _templateRegistry.Render("csharp-model.cshtml", templateData);
    }

    /// <summary>
    /// Получает C# специфичные опции из конфигурации
    /// </summary>
    private static CSharpGenerationOptions GetCSharpOptions(GenerationConfiguration config)
    {
        // Пытаемся получить C# опции из config.Options
        if (config.Options?.CSharpOptions is CSharpGenerationOptions csharpOptions)
        {
            return csharpOptions;
        }
        
        // Используем значения по умолчанию
        return new CSharpGenerationOptions();
    }

    private Dictionary<string, string> GenerateClients(IReadOnlyList<ApiOperation> operations, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver)
    {
        var files = new Dictionary<string, string>();

        // Собираем все уникальные исключения из всех операций
        var exceptionClasses = new HashSet<string>();
        var exceptionDataMap = new Dictionary<string, ExceptionTemplateData>();

        // Группируем операции по тегам или создаем один клиент
        var groupedOperations = GroupOperations(operations, config);

        // Получаем C# опции для определения стиля query параметров
        var csharpOptions = GetCSharpOptions(config);
        var useQueryParametersModel = csharpOptions.QueryParametersStyle == QueryParametersStyle.ModelClass;

        // Собираем информацию о всех исключениях и моделях query параметров
        var queryParametersModels = new Dictionary<string, List<ParameterTemplateData>>();
        foreach (var operation in operations)
        {
            var operationTemplate = CreateOperationTemplateData(operation, document, config, schemaResolver, useQueryParametersModel);
            
            // Собираем модели query параметров для генерации
            if (useQueryParametersModel && operationTemplate.HasQueryParameters && !string.IsNullOrEmpty(operationTemplate.QueryParametersModelClassName))
            {
                if (!queryParametersModels.ContainsKey(operationTemplate.QueryParametersModelClassName))
                {
                    queryParametersModels[operationTemplate.QueryParametersModelClassName] = operationTemplate.QueryParameters.ToList();
                }
            }
            
            foreach (var response in operationTemplate.Responses)
            {
                if (!response.IsSuccess && !string.IsNullOrEmpty(response.ExceptionClassName))
                {
                    if (!exceptionClasses.Contains(response.ExceptionClassName))
                    {
                        exceptionClasses.Add(response.ExceptionClassName);
                        
                        var statusCodeNumber = int.TryParse(response.StatusCode, out var code) ? code : 0;
                        exceptionDataMap[response.ExceptionClassName] = new ExceptionTemplateData
                        {
                            Namespace = config.Namespace ?? "Generated",
                            ClassName = response.ExceptionClassName,
                            StatusCode = response.StatusCode,
                            StatusCodeNumber = statusCodeNumber,
                            Description = response.Description,
                            ResponseTypeInfo = response.ResponseTypeInfo
                        };
                    }
                }
            }
        }

        // Генерируем базовый класс ValioHttpRequestException (только один раз)
        if (exceptionDataMap.Count > 0)
        {
            var baseExceptionCode = GenerateBaseExceptionCode(config.Namespace ?? "Generated");
            var baseExceptionPath = Path.Combine(config.OutputPath, "Exceptions", "ValioHttpRequestException.cs");
            files[baseExceptionPath] = baseExceptionCode;
        }

        // Генерируем классы исключений
        foreach (var exceptionData in exceptionDataMap.Values)
        {
            var exceptionCode = GenerateExceptionCode(exceptionData);
            var fileName = $"{exceptionData.ClassName}.cs";
            
            // Определяем путь для файла исключения
            string filePath;
            if (config.FileOrganization.SeparateClients)
            {
                var clientsPath = config.FileOrganization.ClientsPath ?? "Clients";
                filePath = Path.Combine(config.OutputPath, clientsPath, "Exceptions", fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, "Exceptions", fileName);
            }

            files[filePath] = exceptionCode;
        }

        // Генерируем модели query параметров
        foreach (var queryModel in queryParametersModels)
        {
            var modelCode = GenerateQueryParametersModelCode(queryModel.Key, queryModel.Value, config);
            var fileName = $"{queryModel.Key}.cs";
            
            // Определяем путь для файла модели
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "Models";
                filePath = Path.Combine(config.OutputPath, modelsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = modelCode;
        }

        // Генерируем клиенты
        foreach (var group in groupedOperations)
        {
            var clientCode = GenerateClientCode(group.Value, group.Key, document, config, schemaResolver);
            var fileName = $"{_namingConverter.ToPascalCase(group.Key)}Client.cs";
            
            // Определяем путь для файла клиента
            string filePath;
            if (config.FileOrganization.SeparateClients)
            {
                var clientsPath = config.FileOrganization.ClientsPath ?? "Clients";
                filePath = Path.Combine(config.OutputPath, clientsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = clientCode;
        }

        return files;
    }

    /// <summary>
    /// Генерирует код модели для query параметров
    /// </summary>
    private string GenerateQueryParametersModelCode(string className, List<ParameterTemplateData> parameters, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var csharpOptions = GetCSharpOptions(config);

        var properties = new List<PropertyTemplateData>();
        foreach (var param in parameters)
        {
            properties.Add(new PropertyTemplateData
            {
                Name = _namingConverter.ToPascalCase(param.Name),
                TypeInfo = param.TypeInfo,
                IsRequired = param.IsRequired,
                Description = param.Description,
                AccessorType = csharpOptions.PropertyAccessor,
                IsNullable = !param.IsRequired && !param.TypeInfo.IsNonNullableValueType()
            });
        }

        var templateData = new ModelTemplateData
        {
            Namespace = namespaceName,
            ClassName = className,
            Properties = properties,
            ModelType = csharpOptions.ModelType,
            IsSealed = csharpOptions.UseSealed,
            GenerateEqualityMembers = csharpOptions.GenerateEqualityMembers,
            GenerateValidationConstants = csharpOptions.GenerateValidationConstants,
            GenerateValidationAttributes = csharpOptions.GenerateValidationAttributes
        };

        return _templateRegistry.Render("csharp-model.cshtml", templateData);
    }

    private string GenerateExceptionCode(ExceptionTemplateData exceptionData)
    {
        return _templateRegistry.Render("csharp-exception.cshtml", exceptionData);
    }

    private string GenerateBaseExceptionCode(string namespaceName)
    {
        return $@"using System;
using System.Net.Http;

namespace {namespaceName};

/// <summary>
/// Базовый класс для всех исключений HTTP запросов ValIO
/// </summary>
public class ValioHttpRequestException : HttpRequestException
{{
    /// <summary>
    /// HTTP ответ, вызвавший исключение
    /// </summary>
    public HttpResponseMessage Response {{ get; }}

    /// <summary>
    /// Имя операции, которая вызвала исключение
    /// </summary>
    public string? OperationName {{ get; }}

    /// <summary>
    /// HTTP метод запроса
    /// </summary>
    public string? HttpMethod {{ get; }}

    /// <summary>
    /// Путь запроса
    /// </summary>
    public string? RequestPath {{ get; }}

    /// <summary>
    /// HTTP статус код ответа
    /// </summary>
    public int? StatusCode {{ get; }}

    public ValioHttpRequestException(string message, HttpResponseMessage response) 
        : base(message)
    {{
        Response = response ?? throw new ArgumentNullException(nameof(response));
        StatusCode = (int?)response.StatusCode;
    }}

    public ValioHttpRequestException(string message, HttpResponseMessage response, Exception innerException) 
        : base(message, innerException)
    {{
        Response = response ?? throw new ArgumentNullException(nameof(response));
        StatusCode = (int?)response.StatusCode;
    }}

    public ValioHttpRequestException(string message, HttpResponseMessage response, string? operationName, string? httpMethod, string? requestPath) 
        : base(message)
    {{
        Response = response ?? throw new ArgumentNullException(nameof(response));
        OperationName = operationName;
        HttpMethod = httpMethod;
        RequestPath = requestPath;
        StatusCode = (int?)response.StatusCode;
    }}

    public ValioHttpRequestException(string message, HttpResponseMessage response, Exception innerException, string? operationName, string? httpMethod, string? requestPath) 
        : base(message, innerException)
    {{
        Response = response ?? throw new ArgumentNullException(nameof(response));
        OperationName = operationName;
        HttpMethod = httpMethod;
        RequestPath = requestPath;
        StatusCode = (int?)response.StatusCode;
    }}
}}
";
    }

    private Dictionary<string, List<ApiOperation>> GroupOperations(IReadOnlyList<ApiOperation> operations, GenerationConfiguration config)
    {
        var groups = new Dictionary<string, List<ApiOperation>>();

        switch (config.FileOrganization.GroupingStrategy)
        {
            case FileGroupingStrategy.ByTag:
                foreach (var operation in operations)
                {
                    var tag = operation.Tags.FirstOrDefault() ?? "Default";
                    var sanitizedTag = _namingConverter.SanitizeIdentifier(tag);
                    if (!groups.ContainsKey(sanitizedTag))
                    {
                        groups[sanitizedTag] = new List<ApiOperation>();
                    }
                    groups[sanitizedTag].Add(operation);
                }
                break;

            case FileGroupingStrategy.ByRoute:
                foreach (var operation in operations)
                {
                    // Извлекаем базовый путь из route (например, /api/users -> Users)
                    var routeGroup = ExtractRouteGroup(operation.Path);
                    if (!groups.ContainsKey(routeGroup))
                    {
                        groups[routeGroup] = new List<ApiOperation>();
                    }
                    groups[routeGroup].Add(operation);
                }
                break;

            case FileGroupingStrategy.SingleFile:
            default:
                // SingleFile - все операции в одном клиенте
                groups["Api"] = operations.ToList();
                break;
        }

        return groups;
    }

    /// <summary>
    /// Извлекает имя группы из пути операции
    /// Например: /api/users/{id} -> Users, /api/products -> Products
    /// </summary>
    private string ExtractRouteGroup(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Default";

        // Убираем начальный и конечный слэши
        var cleanPath = path.Trim('/');
        
        // Разбиваем путь на сегменты
        var segments = cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (segments.Length == 0)
            return "Default";

        // Берем последний сегмент пути (или предпоследний, если последний - параметр)
        // Например: /api/users/{id} -> users, /api/products -> products
        var lastSegment = segments[^1];
        
        // Если последний сегмент - параметр (начинается с {), берем предыдущий
        if (lastSegment.StartsWith("{") && segments.Length > 1)
        {
            lastSegment = segments[^2];
        }

        // Убираем префиксы типа "api", "v1", "v2" и т.д.
        if (segments.Length > 1 && (lastSegment.StartsWith("api", StringComparison.OrdinalIgnoreCase) || 
                                     lastSegment.StartsWith("v", StringComparison.OrdinalIgnoreCase)))
        {
            if (segments.Length > 2)
            {
                lastSegment = segments[^2];
            }
        }

        // Конвертируем в PascalCase и санитизируем
        var sanitized = _namingConverter.SanitizeIdentifier(lastSegment);
        return _namingConverter.ToPascalCase(sanitized);
    }

    private string GenerateClientCode(List<ApiOperation> operations, string clientName, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = $"{_namingConverter.ToPascalCase(clientName)}Client";
        var csharpOptions = GetCSharpOptions(config);
        var useQueryParametersModel = csharpOptions.QueryParametersStyle == QueryParametersStyle.ModelClass;

        var operationTemplates = operations.Select(op => CreateOperationTemplateData(op, document, config, schemaResolver, useQueryParametersModel)).ToList();

        var templateData = new ClientTemplateData
        {
            Namespace = namespaceName,
            ClassName = className,
            ClientName = clientName,
            Operations = operationTemplates
        };

        return _templateRegistry.Render("csharp-client.cshtml", templateData);
    }

    private OperationTemplateData CreateOperationTemplateData(ApiOperation operation, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver, bool useQueryParametersModel = false)
    {
        // Валидируем и генерируем имя метода
        var operationId = string.IsNullOrWhiteSpace(operation.OperationId) 
            ? GenerateOperationId(operation.Path, operation.HttpMethod) 
            : operation.OperationId;
        var methodName = _namingConverter.ToPascalCase(operationId);
        
        // Валидируем, что имя метода не пустое
        if (string.IsNullOrWhiteSpace(methodName))
        {
            methodName = "Execute";
            _logger.LogWarning("Generated default method name 'Execute' for operation at path '{Path}' with method '{Method}'", 
                operation.Path, operation.HttpMethod.Method);
        }
        
        var httpMethod = operation.HttpMethod.Method.ToUpperInvariant();
        var path = operation.Path ?? string.Empty;

        // Разделяем параметры по типам
        var pathParameters = new List<ParameterTemplateData>();
        var queryParameters = new List<ParameterTemplateData>();
        var headerParameters = new List<ParameterTemplateData>();

        foreach (var parameter in operation.Parameters)
        {
            var paramName = _namingConverter.ToCamelCase(parameter.Name);
            
            // Получаем TypeInfo для параметра
            TypeInfo paramTypeInfo;
            if (string.IsNullOrEmpty(parameter.Type) || parameter.Type == "object" || parameter.Type == "string")
            {
                // Если тип не определен, создаем простой тип
                paramTypeInfo = TypeInfo.Simple(parameter.Type == "object" ? "object" : "string", isPrimitive: true);
            }
            else
            {
                // Пытаемся найти оригинальную операцию для получения схемы параметра
                var originalOperation = FindOriginalOperation(document, operation);
                if (originalOperation != null && originalOperation.Parameters != null)
                {
                    var openApiParam = originalOperation.Parameters
                        .FirstOrDefault(p => p.Name == parameter.Name);
                    if (openApiParam?.Schema != null)
                    {
                        paramTypeInfo = _csharpTypeMapper.MapPropertyTypeInfo(openApiParam.Schema, parameter.IsRequired, schemaResolver);
                    }
                    else
                    {
                        paramTypeInfo = TypeInfo.Simple(parameter.Type);
                    }
                }
                else
                {
                    paramTypeInfo = TypeInfo.Simple(parameter.Type);
                }
            }

            var paramTemplate = new ParameterTemplateData
            {
                Name = parameter.Name,
                VariableName = paramName,
                PropertyName = _namingConverter.ToPascalCase(parameter.Name),
                TypeInfo = paramTypeInfo,
                IsRequired = parameter.IsRequired,
                Description = DecodeHtmlEntities(parameter.Description)
            };

            switch (parameter.Location)
            {
                case Core.Models.ParameterLocation.Path:
                    pathParameters.Add(paramTemplate);
                    break;
                case Core.Models.ParameterLocation.Query:
                    queryParameters.Add(paramTemplate);
                    break;
                case Core.Models.ParameterLocation.Header:
                    headerParameters.Add(paramTemplate);
                    break;
            }
        }

        // Request body
        var requestBodyParameter = "request";
        TypeInfo? requestBodyTypeInfo = null;
        var requestBodyContentType = "application/json";
        var isFormData = false;
        var formDataProperties = new List<FormDataPropertyTemplateData>();
        
        if (operation.RequestBody != null)
        {
            requestBodyContentType = operation.RequestBody.ContentType ?? "application/json";
            isFormData = operation.RequestBody.IsFormData;
            
            // Если это form-data, создаем параметры для каждого свойства
            if (isFormData && operation.RequestBody.FormDataProperties != null)
            {
                foreach (var formDataPropEntry in operation.RequestBody.FormDataProperties)
                {
                    var formDataProp = formDataPropEntry.Value;
                    var propName = _namingConverter.ToCamelCase(formDataProp.Name);
                    
                    // Получаем TypeInfo для свойства
                    TypeInfo typeInfo;
                    if (formDataProp.TypeInfo is TypeInfo existingTypeInfo)
                    {
                        typeInfo = existingTypeInfo;
                    }
                    else
                    {
                        // Если TypeInfo не установлен, нужно получить схему и маппить
                        // Находим оригинальную операцию для получения схемы
                        var originalOperation = FindOriginalOperation(document, operation);
                        if (originalOperation?.RequestBody?.Content != null)
                        {
                            var content = originalOperation.RequestBody.Content.FirstOrDefault();
                            if (content.Key != null && content.Value?.Schema?.Properties != null)
                            {
                                var propertySchema = content.Value.Schema.Properties
                                    .FirstOrDefault(p => p.Key == formDataProp.Name).Value;
                                if (propertySchema != null)
                                {
                                    typeInfo = _csharpTypeMapper.MapFormDataPropertyTypeInfo(propertySchema, formDataProp.IsRequired, schemaResolver);
                                }
                                else
                                {
                                    typeInfo = TypeInfo.Simple("string", isPrimitive: true);
                                }
                            }
                            else
                            {
                                typeInfo = TypeInfo.Simple("string", isPrimitive: true);
                            }
                        }
                        else
                        {
                            typeInfo = TypeInfo.Simple("string", isPrimitive: true);
                        }
                    }
                    
                    formDataProperties.Add(new FormDataPropertyTemplateData
                    {
                        Name = formDataProp.Name,
                        VariableName = propName,
                        TypeInfo = typeInfo,
                        IsRequired = formDataProp.IsRequired,
                        Description = DecodeHtmlEntities(formDataProp.Description)
                    });
                }
            }
            else
            {
                // Определяем тип request body из схемы
                requestBodyTypeInfo = DetermineRequestBodyTypeInfo(operation, document, schemaResolver);
                
                // Если тип не определен, используем object
                if (requestBodyTypeInfo == null)
                {
                    requestBodyTypeInfo = TypeInfo.Simple("object");
                }
            }
        }

        // Определяем тип ответа на основе успешных ответов (2xx) через TypeInfo
        var returnTypeInfo = DetermineReturnTypeInfo(operation, document, config, schemaResolver);
        
        // Определяем, есть ли тип ответа (не void, не просто Task, не HttpResponseMessage)
        var hasResponseType = returnTypeInfo.IsTask && returnTypeInfo.GenericArguments.Count > 0 &&
                             !returnTypeInfo.GenericArguments[0].IsVoid &&
                             !returnTypeInfo.GenericArguments[0].IsHttpResponseMessage;

        // Собираем информацию о всех ответах (включая ошибки)
        var responses = new List<ResponseTemplateData>();
        var originalOperationForResponses = FindOriginalOperation(document, operation);
        
        if (originalOperationForResponses?.Responses != null)
        {
            foreach (var responseEntry in originalOperationForResponses.Responses)
            {
                var statusCode = responseEntry.Key;
                var openApiResponse = responseEntry.Value;
                
                // Определяем, является ли это успешным ответом
                var isSuccess = statusCode.StartsWith("2") || statusCode == "default";
                
                // Определяем тип ответа для этого статус кода
                TypeInfo? responseTypeInfo = null;
                if (openApiResponse?.Content != null)
                {
                    var contentTypes = new[] { "application/json", "application/xml", "text/json", "text/xml" };
                    var responseSchema = FindSchemaInContent(openApiResponse.Content, contentTypes);
                    
                    if (responseSchema != null)
                    {
                        try
                        {
                            responseTypeInfo = MapSchemaToTypeInfo(responseSchema, document, schemaResolver, operation.OperationId, operation.Path, $"response {statusCode}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to map response type for status code '{StatusCode}' in operation '{OperationId}'", 
                                statusCode, operation.OperationId);
                        }
                    }
                }
                
                // Генерируем имя класса исключения для ошибок
                string? exceptionClassName = null;
                if (!isSuccess && responseTypeInfo != null)
                {
                    // Для ошибок с моделью создаем класс исключения
                    var statusCodeNumber = int.TryParse(statusCode, out var code) ? code : 0;
                    var exceptionName = statusCodeNumber switch
                    {
                        400 => "BadRequest",
                        401 => "Unauthorized",
                        403 => "Forbidden",
                        404 => "NotFound",
                        409 => "Conflict",
                        422 => "UnprocessableEntity",
                        500 => "InternalServerError",
                        502 => "BadGateway",
                        503 => "ServiceUnavailable",
                        _ => $"Http{statusCode}Error"
                    };
                    exceptionClassName = $"{exceptionName}Exception";
                }
                
                responses.Add(new ResponseTemplateData
                {
                    StatusCode = statusCode,
                    Description = DecodeHtmlEntities(openApiResponse?.Description),
                    ResponseTypeInfo = responseTypeInfo,
                    IsSuccess = isSuccess,
                    ExceptionClassName = exceptionClassName
                });
            }
        }

        // Генерируем имя модели для query параметров, если используется модель
        string? queryParametersModelClassName = null;
        if (useQueryParametersModel && queryParameters.Count > 0)
        {
            queryParametersModelClassName = $"{methodName}QueryParameters";
        }

        // Формируем параметры метода в зависимости от стиля
        var methodParameters = new List<string>();
        var usedParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Path параметры всегда передаются отдельно
        foreach (var pathParam in pathParameters)
        {
            // Проверяем на дубликаты
            if (usedParameterNames.Contains(pathParam.VariableName))
            {
                _logger.LogWarning("Duplicate parameter name '{ParameterName}' found in operation '{OperationId}'. Skipping duplicate.", 
                    pathParam.VariableName, operation.OperationId);
                continue;
            }
            usedParameterNames.Add(pathParam.VariableName);
            var paramString = FormatParameterString(pathParam.TypeInfo, pathParam.VariableName, pathParam.IsRequired);
            methodParameters.Add(paramString);
        }
        
        // Query параметры: либо отдельно, либо как модель
        if (useQueryParametersModel && queryParameters.Count > 0)
        {
            // Определяем, должен ли query параметр быть nullable
            // Если хотя бы один query параметр не-nullable (required или non-nullable value type), то query должен быть не-nullable
            // Параметр считается не-nullable если:
            // 1. Он required (IsRequired = true) - обязательный параметр
            // 2. Или это non-nullable value type (например, int, Guid) - не может быть null по определению
            var hasNonNullableParameter = queryParameters.Any(p => 
                p.IsRequired || p.TypeInfo.IsNonNullableValueType());
            var isQueryNullable = !hasNonNullableParameter;
            
            var queryModelType = TypeInfo.Simple(queryParametersModelClassName!);
            var queryParamString = FormatParameterString(queryModelType, "query", !isQueryNullable);
            methodParameters.Add(queryParamString);
        }
        else
        {
            foreach (var queryParam in queryParameters)
            {
                // Проверяем на дубликаты (не должно быть конфликта с path параметрами)
                if (usedParameterNames.Contains(queryParam.VariableName))
                {
                    _logger.LogWarning("Duplicate parameter name '{ParameterName}' found in operation '{OperationId}'. Skipping duplicate.", 
                        queryParam.VariableName, operation.OperationId);
                    continue;
                }
                usedParameterNames.Add(queryParam.VariableName);
                var paramString = FormatParameterString(queryParam.TypeInfo, queryParam.VariableName, queryParam.IsRequired);
                methodParameters.Add(paramString);
            }
        }
        
        // Header параметры всегда передаются отдельно
        foreach (var headerParam in headerParameters)
        {
            // Проверяем на дубликаты
            if (usedParameterNames.Contains(headerParam.VariableName))
            {
                _logger.LogWarning("Duplicate parameter name '{ParameterName}' found in operation '{OperationId}'. Skipping duplicate.", 
                    headerParam.VariableName, operation.OperationId);
                continue;
            }
            usedParameterNames.Add(headerParam.VariableName);
            var paramString = FormatParameterString(headerParam.TypeInfo, headerParam.VariableName, headerParam.IsRequired);
            methodParameters.Add(paramString);
        }
        
        // Request body
        if (operation.RequestBody != null)
        {
            if (isFormData && formDataProperties.Count > 0)
            {
                // Form-data параметры передаются отдельно
                foreach (var formDataProp in formDataProperties)
                {
                    // Проверяем на дубликаты
                    if (usedParameterNames.Contains(formDataProp.VariableName))
                    {
                        _logger.LogWarning("Duplicate parameter name '{ParameterName}' found in operation '{OperationId}'. Skipping duplicate.", 
                            formDataProp.VariableName, operation.OperationId);
                        continue;
                    }
                    usedParameterNames.Add(formDataProp.VariableName);
                    var paramString = FormatParameterString(formDataProp.TypeInfo, formDataProp.VariableName, formDataProp.IsRequired);
                    methodParameters.Add(paramString);
                }
            }
            else
            {
                // Request body передается как один параметр
                var paramString = FormatParameterString(requestBodyTypeInfo ?? TypeInfo.Simple("object"), requestBodyParameter, operation.RequestBody.IsRequired);
                methodParameters.Add(paramString);
            }
        }

        return new OperationTemplateData
        {
            MethodName = methodName,
            HttpMethod = httpMethod,
            Path = path,
            Summary = DecodeHtmlEntities(operation.Summary),
            Parameters = string.Join(", ", methodParameters),
            ReturnTypeInfo = returnTypeInfo,
            HasRequestBody = operation.RequestBody != null,
            RequestBodyParameter = requestBodyParameter,
            RequestBodyTypeInfo = requestBodyTypeInfo,
            RequestBodyContentType = requestBodyContentType,
            HasResponseType = hasResponseType,
            HasPathParameters = pathParameters.Count > 0,
            HasQueryParameters = queryParameters.Count > 0,
            HasHeaderParameters = headerParameters.Count > 0,
            PathParameters = pathParameters,
            QueryParameters = queryParameters,
            HeaderParameters = headerParameters,
            IsFormData = isFormData,
            FormDataProperties = formDataProperties.Count > 0 ? formDataProperties : null,
            Responses = responses,
            QueryParametersModelClassName = queryParametersModelClassName,
            UseQueryParametersModel = useQueryParametersModel
        };
    }

    /// <summary>
    /// Определяет тип возврата операции через TypeInfo
    /// </summary>
    private TypeInfo DetermineReturnTypeInfo(ApiOperation operation, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver)
    {
        // Ищем успешные ответы (2xx), приоритет: 200, 201, 202, 204, default
        var successResponseKeys = new[] { "200", "201", "202", "204", "default" };
        var successResponse = operation.Responses
            .FirstOrDefault(r => successResponseKeys.Contains(r.Key));

        if (successResponse.Key == null)
        {
            // Если нет успешных ответов, ищем любой ответ
            successResponse = operation.Responses.FirstOrDefault();
        }

        if (successResponse.Key == null)
        {
            // Если нет ответов, возвращаем HttpResponseMessage
            return TypeInfo.Task(TypeInfo.HttpResponseMessage());
        }

        var response = successResponse.Value;
        
        // Находим оригинальную операцию в документе для получения схемы ответа
        var originalOperation = FindOriginalOperation(document, operation);
        if (originalOperation != null && originalOperation.Responses != null)
        {
            // Ищем ответ по статус-коду
            var openApiResponse = originalOperation.Responses
                .FirstOrDefault(r => r.Key == successResponse.Key);
            
            if (openApiResponse.Key != null && openApiResponse.Value?.Content != null)
            {
                // Пробуем найти схему в разных content types (приоритет: application/json)
                var contentTypes = new[] { "application/json", "application/xml", "text/json", "text/xml" };
                var responseSchema = FindSchemaInContent(openApiResponse.Value.Content, contentTypes);
                
                if (responseSchema != null)
                {
                    try
                    {
                        var typeInfo = MapSchemaToTypeInfo(responseSchema, document, schemaResolver, operation.OperationId, operation.Path, "response");
                        if (typeInfo != null)
                        {
                            return TypeInfo.Task(typeInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to map response type for operation '{OperationId}' at path '{Path}'. Using HttpResponseMessage.", 
                            operation.OperationId, operation.Path);
                    }
                }
            }
        }

        // Если тип не определен через схему, используем строковый тип из ApiResponse
        var responseType = response.Type;
        
        if (string.IsNullOrEmpty(responseType))
        {
            // Если тип не определен, возвращаем HttpResponseMessage
            return TypeInfo.Task(TypeInfo.HttpResponseMessage());
        }

        // Если тип void или пустой, возвращаем Task
        if (responseType == "void" || responseType == "null")
        {
            return TypeInfo.Task();
        }

        // Создаем простой тип из строки (для обратной совместимости)
        var resultTypeInfo = TypeInfo.Simple(responseType);
        return TypeInfo.Task(resultTypeInfo);
    }

    /// <summary>
    /// Определяет тип request body операции через TypeInfo
    /// </summary>
    private TypeInfo? DetermineRequestBodyTypeInfo(ApiOperation operation, OpenApiDocument document, ISchemaResolver schemaResolver)
    {
        if (operation.RequestBody == null)
        {
            return null;
        }

        // Находим оригинальную операцию в документе для получения схемы request body
        var originalOperation = FindOriginalOperation(document, operation);
        if (originalOperation?.RequestBody?.Content != null)
        {
            // Пробуем найти схему в разных content types (приоритет: application/json)
            var contentTypes = new[] { "application/json", "application/xml", "text/json", "text/xml", "application/octet-stream" };
            var requestBodySchema = FindSchemaInContent(originalOperation.RequestBody.Content, contentTypes);
            
            if (requestBodySchema != null)
            {
                try
                {
                    var typeInfo = MapSchemaToTypeInfo(requestBodySchema, document, schemaResolver, operation.OperationId, operation.Path, "request body");
                    if (typeInfo != null)
                    {
                        _logger.LogDebug("Mapped request body type '{TypeName}' for operation '{OperationId}' at path '{Path}'", 
                            typeInfo.FullTypeName, operation.OperationId, operation.Path);
                        return typeInfo;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to map request body type for operation '{OperationId}' at path '{Path}' (Method: {Method}). Using object. Error: {Error}", 
                        operation.OperationId, operation.Path, operation.HttpMethod.Method, ex.Message);
                }
            }
        }

        // Если тип не определен через схему, используем строковый тип из ApiRequestBody
        var requestBodyType = operation.RequestBody.Type;
        
        if (string.IsNullOrEmpty(requestBodyType) || requestBodyType == "object")
        {
            // Если тип не определен, возвращаем null (будет использован object)
            return null;
        }

        // Создаем простой тип из строки (для обратной совместимости)
        var resultTypeInfo = TypeInfo.Simple(requestBodyType);
        return resultTypeInfo;
    }

    /// <summary>
    /// Находит схему в содержимом по приоритету content types
    /// </summary>
    private IOpenApiSchema? FindSchemaInContent(IDictionary<string, IOpenApiMediaType> content, string[] preferredContentTypes)
    {
        // Сначала ищем по приоритету
        foreach (var contentType in preferredContentTypes)
        {
            if (content.TryGetValue(contentType, out var mediaType) && mediaType?.Schema != null)
            {
                return mediaType.Schema;
            }
        }
        
        // Если не нашли по приоритету, берем первый доступный
        var firstContent = content.FirstOrDefault();
        if (firstContent.Key != null && firstContent.Value?.Schema != null)
        {
            return firstContent.Value.Schema;
        }
        
        return null;
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с использованием различных стратегий поиска
    /// </summary>
    private TypeInfo? MapSchemaToTypeInfo(
        IOpenApiSchema schema, 
        OpenApiDocument document, 
        ISchemaResolver schemaResolver, 
        string? operationId, 
        string? path, 
        string context)
    {
        // Проверяем кэш по ссылке на схему
        string? cacheKey = null;
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
        {
            var reference = refHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                cacheKey = reference.Id;
                if (_schemaTypeInfoCache != null && _schemaTypeInfoCache.TryGetValue(cacheKey, out var cachedTypeInfo))
                {
                    return cachedTypeInfo;
                }
            }
        }
        
        // Стратегия 1: Проверяем, является ли схема ссылкой напрямую
        string? schemaName = null;
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder2)
        {
            var reference = refHolder2.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                schemaName = reference.Id;
            }
        }
        
        // Стратегия 2: Если не нашли через прямую проверку, используем GetSchemaName
        if (string.IsNullOrEmpty(schemaName))
        {
            schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        }
        
        // Стратегия 3: Если имя найдено, используем его напрямую
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            var typeName = _namingConverter.ToPascalCase(schemaName);
            var typeInfo = TypeInfo.Simple(typeName);
            
            // Сохраняем в кэш
            if (cacheKey != null && _schemaTypeInfoCache != null)
            {
                _schemaTypeInfoCache[cacheKey] = typeInfo;
            }
            
            return typeInfo;
        }
        
        // Стратегия 4: Если имя не найдено через ссылку, пытаемся найти через маппинг
        var mappedTypeInfo = _csharpTypeMapper.MapTypeInfo(schema, schemaResolver);
        
        // Проверяем, что это не просто "object"
        if (!mappedTypeInfo.IsVoid && 
            !mappedTypeInfo.IsHttpResponseMessage && 
            mappedTypeInfo.FullTypeName != "object")
        {
            // Сохраняем в кэш
            if (cacheKey != null && _schemaTypeInfoCache != null)
            {
                _schemaTypeInfoCache[cacheKey] = mappedTypeInfo;
            }
            
            return mappedTypeInfo;
        }
        
        // Стратегия 5: Если все еще "object", пытаемся найти имя схемы в документе
        if (mappedTypeInfo.FullTypeName == "object" && document.Components?.Schemas != null)
        {
            var resolvedSchema = schemaResolver.ResolveSchema(schema);
            foreach (var schemaEntry in document.Components.Schemas)
            {
                var componentSchema = schemaResolver.ResolveSchema(schemaEntry.Value);
                // Сравниваем схемы по содержимому (упрощенная проверка)
                if (ReferenceEquals(componentSchema, resolvedSchema) || 
                    AreSchemasSimilar(componentSchema, resolvedSchema))
                {
                    var foundTypeName = _namingConverter.ToPascalCase(schemaEntry.Key);
                    var foundTypeInfo = TypeInfo.Simple(foundTypeName);
                    
                    // Сохраняем в кэш
                    if (cacheKey != null && _schemaTypeInfoCache != null)
                    {
                        _schemaTypeInfoCache[cacheKey] = foundTypeInfo;
                    }
                    
                    return foundTypeInfo;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Проверяет, похожи ли две схемы по содержимому
    /// </summary>
    private bool AreSchemasSimilar(IOpenApiSchema schema1, IOpenApiSchema schema2)
    {
        if (schema1 == null || schema2 == null)
            return false;
        
        // Проверяем типы
        if (schema1.Type != schema2.Type)
        {
            return false;
        }
        
        // Проверяем по количеству свойств
        if (schema1.Properties != null && schema2.Properties != null)
        {
            if (schema1.Properties.Count != schema2.Properties.Count)
            {
                return false;
            }
            
            // Проверяем, что все ключи совпадают
            var keys1 = schema1.Properties.Keys.OrderBy(k => k).ToList();
            var keys2 = schema2.Properties.Keys.OrderBy(k => k).ToList();
            
            if (!keys1.SequenceEqual(keys2))
            {
                return false;
            }
            
            // Дополнительно проверяем типы свойств (упрощенная проверка)
            foreach (var key in keys1)
            {
                var prop1 = schema1.Properties[key];
                var prop2 = schema2.Properties[key];
                
                if (prop1?.Type != prop2?.Type)
                {
                    return false;
                }
            }
        }
        else if (schema1.Properties != null || schema2.Properties != null)
        {
            return false;
        }
        
        // Проверяем формат
        if (schema1.Format != schema2.Format)
        {
            return false;
        }
        
        // Проверяем, являются ли обе схемы ссылками на один и тот же тип
        if (schema1 is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder1 &&
            schema2 is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder2)
        {
            var ref1 = refHolder1.Reference;
            var ref2 = refHolder2.Reference;
            
            if (ref1 != null && ref2 != null && !string.IsNullOrEmpty(ref1.Id) && !string.IsNullOrEmpty(ref2.Id))
            {
                return ref1.Id == ref2.Id;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Генерирует OperationId на основе пути и HTTP метода, если он не указан
    /// </summary>
    private string GenerateOperationId(string path, HttpMethod httpMethod)
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"{httpMethod.Method}Request";
        
        // Извлекаем последний сегмент пути
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var lastSegment = segments.Length > 0 ? segments[^1] : "request";
        
        // Убираем параметры из пути (например, {id} -> id)
        lastSegment = lastSegment.Replace("{", "").Replace("}", "");
        
        // Комбинируем метод и сегмент
        var methodPrefix = httpMethod.Method.ToUpperInvariant();
        return $"{methodPrefix}{_namingConverter.ToPascalCase(lastSegment)}";
    }

    /// <summary>
    /// Форматирует строку параметра из TypeInfo
    /// </summary>
    private string FormatParameterString(TypeInfo typeInfo, string parameterName, bool isRequired)
    {
        var typeString = typeInfo.FullTypeName;
        var nullableModifier = !isRequired && !typeInfo.IsNullable ? "?" : "";
        
        // Если тип уже nullable, не добавляем "?"
        if (typeInfo.IsNullable && typeString.EndsWith("?"))
        {
            nullableModifier = "";
        }
        
        return $"{typeString}{nullableModifier} {parameterName}";
    }

    /// <summary>
    /// Валидирует операции на дублирующиеся operationId и другие проблемы
    /// </summary>
    private void ValidateOperations(IReadOnlyList<ApiOperation> operations, GenerationResult result)
    {
        var operationIds = new Dictionary<string, List<string>>();
        var warnings = result.Warnings.ToList();
        
        foreach (var operation in operations)
        {
            var operationId = string.IsNullOrWhiteSpace(operation.OperationId) 
                ? GenerateOperationId(operation.Path, operation.HttpMethod) 
                : operation.OperationId;
            
            // Проверяем на пустой operationId
            if (string.IsNullOrWhiteSpace(operationId))
            {
                var warning = $"Operation at path '{operation.Path}' with method '{operation.HttpMethod.Method}' has empty operationId";
                warnings.Add(warning);
                _logger.LogWarning(warning);
                continue;
            }
            
            // Проверяем на дублирующиеся operationId
            var key = $"{operationId}";
            if (!operationIds.ContainsKey(key))
            {
                operationIds[key] = new List<string>();
            }
            operationIds[key].Add($"{operation.HttpMethod.Method} {operation.Path}");
        }
        
        // Находим дубликаты
        foreach (var kvp in operationIds)
        {
            if (kvp.Value.Count > 1)
            {
                var warning = $"Duplicate operationId '{kvp.Key}' found at: {string.Join(", ", kvp.Value)}";
                warnings.Add(warning);
                _logger.LogWarning(warning);
            }
        }
        
        result.Warnings = warnings;
    }

    /// <summary>
    /// Валидирует модели на пустые имена и другие проблемы
    /// </summary>
    private void ValidateModels(IReadOnlyList<ApiModel> models, GenerationResult result)
    {
        var modelNames = new HashSet<string>();
        var warnings = result.Warnings.ToList();
        
        foreach (var model in models)
        {
            // Проверяем на пустое имя
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                var warning = "Found model with empty name, skipping";
                warnings.Add(warning);
                _logger.LogWarning(warning);
                continue;
            }
            
            // Проверяем на дублирующиеся имена (после конвертации в PascalCase)
            var modelName = _namingConverter.ToPascalCase(model.Name);
            if (modelNames.Contains(modelName))
            {
                var warning = $"Duplicate model name '{modelName}' found (original: '{model.Name}')";
                warnings.Add(warning);
                _logger.LogWarning(warning);
            }
            else
            {
                modelNames.Add(modelName);
            }
        }
        
        result.Warnings = warnings;
    }

    private async Task EnsureTemplatesPreloadedAsync()
    {
        if (_templatesPreloaded)
            return;

        try
        {
            // Загружаем шаблоны
            if (!_templateRegistry.HasTemplate("csharp-model.cshtml") || 
                !_templateRegistry.HasTemplate("csharp-client.cshtml") ||
                !_templateRegistry.HasTemplate("csharp-enum.cshtml"))
            {
                await _templateRegistry.PreloadTemplatesAsync();
            }

            _templatesPreloaded = true;
            _logger.LogInformation("Templates preloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload templates");
            throw;
        }
    }
}

