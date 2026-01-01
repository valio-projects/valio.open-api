using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Core.Factories;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.CSharp.Models;
using System.Linq;
using System.Reflection;
using System.Text;
using TypeInfo = ValIO.Generators.CSharp.Models.TypeInfo;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Базовый генератор кода для TypeScript
/// </summary>
public class TypeScriptCodeGenerator : ICodeGenerator
{
    private readonly ITypeMapper _typeMapper;
    private readonly TypeScriptTypeMapper _typescriptTypeMapper;
    private readonly INamingConverter _namingConverter;
    private readonly IFileWriter _fileWriter;
    private readonly ITemplateRegistry _templateRegistry;
    private readonly TemplateRegistryFactory? _templateRegistryFactory;
    private readonly ILogger<TypeScriptCodeGenerator> _logger;
    private bool _templatesPreloaded;
    
    // Кэш для поиска операций (path + method -> operation)
    private Dictionary<string, OpenApiOperation>? _operationCache;
    
    // Кэш для маппинга схем в TypeInfo (schema reference -> TypeInfo)
    private Dictionary<string, TypeInfo>? _schemaTypeInfoCache;

    public TypeScriptCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ITemplateRegistry templateRegistry,
        ILogger<TypeScriptCodeGenerator> logger,
        TemplateRegistryFactory? templateRegistryFactory = null)
    {
        _typeMapper = typeMapper;
        _typescriptTypeMapper = typeMapper as TypeScriptTypeMapper ?? throw new InvalidOperationException("TypeMapper must be TypeScriptTypeMapper");
        _namingConverter = namingConverter;
        _fileWriter = fileWriter;
        _templateRegistry = templateRegistry ?? throw new ArgumentNullException(nameof(templateRegistry));
        _templateRegistryFactory = templateRegistryFactory;
        _logger = logger;
        _templatesPreloaded = false;
    }

    public bool SupportsLanguage(GenerationLanguage language)
    {
        return language == GenerationLanguage.TypeScript;
    }

    public bool SupportsOptions(GenerationOptions? options)
    {
        // Базовый TypeScript генератор поддерживает базовые опции
        // Специализированные генераторы будут проверять конкретные комбинации
        return true;
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

            _logger.LogInformation("Starting TypeScript code generation");

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

            // Получаем TypeScript опции
            var typescriptOptions = GetTypeScriptOptions(config);

            // Генерируем файлы
            var generatedFiles = new Dictionary<string, string>();
            var generationErrors = new List<string>();
            var generationWarnings = new List<string>();

            if (config.GenerateEnums)
            {
                try
                {
                    var enumFiles = GenerateEnums(enums, config, typescriptOptions);
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
                    var modelFiles = GenerateModels(models, config, document, schemaResolver, typescriptOptions);
                foreach (var file in modelFiles)
                {
                    generatedFiles[file.Key] = file.Value;
                    }
                    
                    // Если ModelFormat = ValidationSchemas, генерируем также Zod схемы
                    if (typescriptOptions.ModelFormat == ModelFormat.ValidationSchemas && 
                        typescriptOptions.ValidationLibrary == ValidationLibrary.Zod)
                    {
                        try
                        {
                            var zodSchemaFiles = await GenerateZodSchemasAsync(models, enums, config, document, schemaResolver, typescriptOptions);
                            foreach (var file in zodSchemaFiles)
                            {
                                generatedFiles[file.Key] = file.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"Failed to generate Zod schemas: {ex.Message}";
                            if (ex.InnerException != null)
                            {
                                errorMsg += $" (Inner: {ex.InnerException.Message})";
                            }
                            generationWarnings.Add(errorMsg);
                            _logger.LogWarning(ex, "Failed to generate Zod schemas");
                        }
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
                    // Всегда генерируем базовые клиенты (axios или fetch) в отдельные файлы
                    var clientFiles = GenerateClients(operations, document, config, schemaResolver, typescriptOptions);
                    foreach (var file in clientFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                    
                    // Если UseTanStackQuery = true, дополнительно генерируем TanStack Query хуки
                    if (typescriptOptions.UseTanStackQuery)
                    {
                        var queryFiles = await GenerateTanStackQueryHooksAsync(operations, document, config, schemaResolver, typescriptOptions);
                        foreach (var file in queryFiles)
                        {
                            generatedFiles[file.Key] = file.Value;
                        }
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
            _logger.LogInformation("TypeScript code generation completed successfully. Generated {Count} files", generatedFiles.Count);
            }
            else
            {
                _logger.LogWarning("TypeScript code generation completed with errors. Generated {Count} files, {ErrorCount} errors", 
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
                _logger.LogError(ex, "Error during TypeScript code generation. Stack trace: {StackTrace}", ex.StackTrace);
            }
            else
        {
            _logger.LogError(ex, "Error during TypeScript code generation");
            }
            result.Errors = new List<string> { errorMessage };
            result.Success = false;
        }

        return result;
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
            return errors;
        }

        if (string.IsNullOrWhiteSpace(config.OutputPath))
        {
            errors.Add("OutputPath is required in configuration");
        }

        if (config.Language != GenerationLanguage.TypeScript)
        {
            errors.Add($"Language must be TypeScript, but got {config.Language}");
        }

        return errors;
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

                var cacheKey = $"{operationId}|{operationEntry.Key.Method}|{pathEntry.Key}";
                _operationCache[cacheKey] = openApiOperation;
            }
        }
        
        _logger.LogDebug("Built operation cache with {Count} entries", _operationCache.Count);
    }

    /// <summary>
    /// Находит оригинальную операцию в документе
    /// </summary>
    private OpenApiOperation? FindOriginalOperation(OpenApiDocument document, ApiOperation operation)
    {
        if (_operationCache != null)
        {
            var operationId = operation.OperationId ?? GenerateOperationId(operation.Path, operation.HttpMethod);
            var cacheKey = $"{operationId}|{operation.HttpMethod.Method}|{operation.Path}";
            
            if (_operationCache.TryGetValue(cacheKey, out var cachedOperation))
            {
                return cachedOperation;
            }
        }

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

    private string GenerateOperationId(string path, HttpMethod httpMethod)
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"{httpMethod.Method}Request";
        
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var lastSegment = segments.Length > 0 ? segments[^1] : "request";
        lastSegment = lastSegment.Replace("{", "").Replace("}", "");
        
        var methodPrefix = httpMethod.Method.ToUpperInvariant();
        return $"{methodPrefix}{_namingConverter.ToPascalCase(lastSegment)}";
    }

    private Dictionary<string, string> GenerateEnums(IReadOnlyList<ApiEnum> enums, GenerationConfiguration config, TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>();

        foreach (var apiEnum in enums)
        {
            if (string.IsNullOrWhiteSpace(apiEnum.Name))
            {
                _logger.LogWarning("Skipping enum with empty name");
                continue;
            }
            
            var enumCode = GenerateEnumCode(apiEnum, config, options);
            var fileName = $"{_namingConverter.ToCamelCase(apiEnum.Name)}.enum.ts";
            
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "models";
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

    private string GenerateEnumCode(ApiEnum apiEnum, GenerationConfiguration config, TypeScriptGenerationOptions options)
    {
        var enumName = _namingConverter.ToPascalCase(apiEnum.Name);

        var enumValues = apiEnum.Values.Select(v => new TypeScriptEnumValueTemplateData
        {
            Name = _namingConverter.ToPascalCase(v.Name),
            Value = v.Value,
            Description = DecodeHtmlEntities(v.Description),
            ValueString = FormatEnumValue(v.Value, apiEnum.UnderlyingType, options.EnumFormat)
        }).ToList();

        var templateData = new TypeScriptEnumTemplateData
        {
            EnumName = enumName,
            Description = DecodeHtmlEntities(apiEnum.Description),
            EnumFormat = options.EnumFormat,
            Values = enumValues
        };

        return _templateRegistry.Render("typescript-enum.cshtml", templateData);
    }

    private static string? DecodeHtmlEntities(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static string FormatEnumValue(object? value, string underlyingType, EnumFormat enumFormat)
    {
        if (value == null)
            return enumFormat == EnumFormat.Enum ? "0" : "\"\"";

        if (enumFormat == EnumFormat.Enum)
        {
            return underlyingType switch
            {
                "string" => $"\"{value}\"",
                _ => value.ToString() ?? "0"
            };
        }
        else
        {
            return $"\"{value}\"";
        }
    }

    private Dictionary<string, string> GenerateModels(IReadOnlyList<ApiModel> models, GenerationConfiguration config, OpenApiDocument document, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>();

        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                _logger.LogWarning("Skipping model with empty name");
                continue;
            }
            
            var modelCode = GenerateModelCode(model, config, document, schemaResolver, options);
            var fileName = $"{_namingConverter.ToCamelCase(model.Name)}.model.ts";
            
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "models";
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

    private string GenerateModelCode(ApiModel model, GenerationConfiguration config, OpenApiDocument document, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var interfaceName = _namingConverter.ToPascalCase(model.Name);

        var properties = new List<TypeScriptPropertyTemplateData>();
        foreach (var p in model.Properties)
        {
            TypeInfo typeInfo = TypeInfo.Simple("any");
            try
            {
                // Используем PropertySchemas, который включает свойства из allOf
                if (model.PropertySchemas != null &&
                    model.PropertySchemas.TryGetValue(p.Name, out var propertySchema))
                {
                    typeInfo = _typescriptTypeMapper.MapPropertyTypeInfo(propertySchema, p.IsRequired, schemaResolver);
                }
                // Fallback на OriginalSchema.Properties для обратной совместимости
                else if (model.OriginalSchema?.Properties != null &&
                    model.OriginalSchema.Properties.TryGetValue(p.Name, out var propertySchemaFromOriginal))
                {
                    typeInfo = _typescriptTypeMapper.MapPropertyTypeInfo(propertySchemaFromOriginal, p.IsRequired, schemaResolver);
                }
                else
                {
                    typeInfo = TypeInfo.Simple(p.Type);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map type for property '{PropertyName}' in model '{ModelName}'", p.Name, model.Name);
                typeInfo = TypeInfo.Simple("any");
            }
            
            properties.Add(new TypeScriptPropertyTemplateData
        {
            Name = _namingConverter.ToCamelCase(p.Name),
                TypeInfo = typeInfo,
                Type = typeInfo.FullTypeName,
                IsOptional = !p.IsRequired,
                Description = DecodeHtmlEntities(p.Description)
            });
        }

        // Разделяем свойства на базовые (из allOf) и собственные
        var baseClassProperties = new List<TypeScriptPropertyTemplateData>();
        var ownProperties = new List<TypeScriptPropertyTemplateData>();
        string? baseClassName = null;
        
        if (model.BaseClassName != null && model.BaseClassProperties != null)
        {
            baseClassName = model.BaseClassName;
            var baseClassPropertyNames = model.BaseClassProperties;
            
            // Создаем словарь для быстрого поиска: имя свойства в camelCase -> является ли базовым
            var baseClassPropertyNamesCamelCase = new HashSet<string>(
                baseClassPropertyNames.Select(name => _namingConverter.ToCamelCase(name)),
                StringComparer.OrdinalIgnoreCase);
            
            foreach (var prop in properties)
            {
                // Проверяем, является ли свойство базовым (из allOf)
                // prop.Name уже в camelCase, baseClassPropertyNames в исходном формате из схемы
                if (baseClassPropertyNamesCamelCase.Contains(prop.Name))
                {
                    baseClassProperties.Add(prop);
                }
                else
                {
                    ownProperties.Add(prop);
                }
            }
        }
        else
        {
            // Если нет базового класса, все свойства собственные
            ownProperties = properties;
        }

        // Собираем связанные типы для импортов (включая базовый класс, если есть)
        var importedTypes = CollectModelUsedTypes(properties, interfaceName);
        if (!string.IsNullOrEmpty(baseClassName) && !importedTypes.Contains(baseClassName))
        {
            importedTypes.Add(baseClassName);
        }

        var templateData = new TypeScriptModelTemplateData
        {
            InterfaceName = interfaceName,
            Description = DecodeHtmlEntities(model.Description),
            Properties = properties,
            UseStrictNullChecks = options.UseStrictNullChecks,
            ImportedTypes = importedTypes,
            BaseClassName = baseClassName,
            BaseClassProperties = baseClassProperties,
            OwnProperties = ownProperties
        };

        return _templateRegistry.Render("typescript-model.cshtml", templateData);
    }
    
    /// <summary>
    /// Собирает все используемые типы из свойств модели для импортов
    /// </summary>
    private List<string> CollectModelUsedTypes(IReadOnlyList<TypeScriptPropertyTemplateData> properties, string currentModelName)
    {
        var usedTypes = new HashSet<string>();

        foreach (var property in properties)
        {
            CollectModelTypesFromTypeInfo(property.TypeInfo, usedTypes, currentModelName);
        }

        return usedTypes.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Рекурсивно собирает типы из TypeInfo для моделей
    /// </summary>
    private void CollectModelTypesFromTypeInfo(TypeInfo typeInfo, HashSet<string> usedTypes, string currentModelName)
    {
        if (typeInfo == null)
            return;
        
        var baseType = typeInfo.BaseType;
        
        // Пропускаем примитивные типы и встроенные типы
        if (typeInfo.IsPrimitive || 
            typeInfo.IsVoid ||
            typeInfo.IsFile ||
            string.IsNullOrEmpty(baseType))
        {
            // Но все равно проверяем generic аргументы
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectModelTypesFromTypeInfo(genericArg, usedTypes, currentModelName);
            }
            return;
        }
        
        // Пропускаем встроенные типы TypeScript (включая Record, Array и т.д.)
        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string", "number", "boolean", "any", "void", "null", "undefined",
            "Date", "Blob", "File", "FormData", "Record", "Array", "object"
        };
        
        var fullTypeName = typeInfo.FullTypeName;
        
        // Проверяем, является ли это системным типом TypeScript
        // Record<string, any> или Record<string, T> - это системный тип
        // BaseType может быть "Record<string, any>" (когда создается через TypeInfo.Simple)
        // или просто "Record" (когда создается через TypeInfo.Dictionary)
        var isBuiltInType = builtInTypes.Contains(baseType) || 
                           baseType.StartsWith("Record<", StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrEmpty(fullTypeName) && fullTypeName.StartsWith("Record<", StringComparison.OrdinalIgnoreCase)) ||
                           (baseType.Contains("[]") && builtInTypes.Contains(baseType.Replace("[]", "").Trim()));
        
        if (isBuiltInType)
        {
            // Для Dictionary (Record<K, V>) извлекаем типы из generic аргументов
            if (typeInfo.IsDictionary)
            {
                // Для Dictionary GenericArguments[0] - это key, GenericArguments[1] - это value
                // Key всегда string для Record, пропускаем его
                if (typeInfo.GenericArguments.Count > 1)
                {
                    CollectModelTypesFromTypeInfo(typeInfo.GenericArguments[1], usedTypes, currentModelName);
                }
            }
            // Для Collection (Array<T>) извлекаем типы из generic аргументов
            else if (typeInfo.IsCollection)
            {
                foreach (var genericArg in typeInfo.GenericArguments)
                {
                    CollectModelTypesFromTypeInfo(genericArg, usedTypes, currentModelName);
                }
            }
            // Также проверяем FullTypeName для извлечения типов из Record<string, T>
            // Но только если это не Record<string, any> (any - это примитив)
            else if (!string.IsNullOrEmpty(fullTypeName) && 
                     fullTypeName.Contains("<") && 
                     fullTypeName.StartsWith("Record<", StringComparison.OrdinalIgnoreCase) &&
                     !fullTypeName.Contains("Record<string, any>"))
            {
                ExtractTypesFromGenericString(fullTypeName, usedTypes, currentModelName);
            }
            return;
        }
        
        // Пропускаем текущую модель (чтобы избежать циклических импортов)
        if (string.Equals(baseType, currentModelName, StringComparison.OrdinalIgnoreCase))
        {
            // Но проверяем generic аргументы
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectModelTypesFromTypeInfo(genericArg, usedTypes, currentModelName);
            }
            return;
        }

        // Если это не примитив и не текущая модель, добавляем в список
        // Проверяем, что тип начинается с заглавной буквы (это пользовательский тип)
        if (!string.IsNullOrEmpty(baseType) && char.IsUpper(baseType[0]))
        {
            usedTypes.Add(baseType);
        }

        // Рекурсивно обрабатываем generic аргументы (для Dictionary, Collection и т.д.)
        foreach (var genericArg in typeInfo.GenericArguments)
        {
            CollectModelTypesFromTypeInfo(genericArg, usedTypes, currentModelName);
        }
        
        // Также обрабатываем FullTypeName для извлечения типов из строкового представления
        // Например, для "PagingResult<ItemType>" нужно извлечь "PagingResult" и "ItemType"
        if (!string.IsNullOrEmpty(typeInfo.FullTypeName) && 
            typeInfo.FullTypeName.Contains("<") && 
            !typeInfo.IsDictionary && 
            !typeInfo.IsCollection)
        {
            // Извлекаем типы из generic синтаксиса
            ExtractTypesFromGenericString(typeInfo.FullTypeName, usedTypes, currentModelName);
        }
    }
    
    /// <summary>
    /// Извлекает типы из строкового представления generic типа (например, "PagingResult<ItemType>")
    /// </summary>
    private void ExtractTypesFromGenericString(string typeString, HashSet<string> usedTypes, string currentModelName)
    {
        // Убираем nullable маркеры
        typeString = typeString.Replace("?", "").Trim();
        
        // Находим позицию <
        var genericStart = typeString.IndexOf('<');
        if (genericStart < 0)
            return;
        
        // Извлекаем базовое имя типа
        var baseTypeName = typeString.Substring(0, genericStart).Trim();
        
        // Пропускаем системные типы
        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Record", "Array", "Promise", "Task"
        };
        
        if (builtInTypes.Contains(baseTypeName))
        {
            // Для системных типов извлекаем только generic аргументы
            var genericPart = typeString.Substring(genericStart + 1);
            genericPart = genericPart.TrimEnd('>');
            
            // Разделяем по запятым, учитывая вложенные generic
            var types = SplitGenericArguments(genericPart);
            foreach (var type in types)
            {
                var cleanType = type.Trim().Split('<')[0].Split('[')[0].Trim();
                if (!string.IsNullOrEmpty(cleanType) && 
                    char.IsUpper(cleanType[0]) && 
                    !string.Equals(cleanType, currentModelName, StringComparison.OrdinalIgnoreCase))
                {
                    usedTypes.Add(cleanType);
                }
            }
            return;
        }
        
        // Для пользовательских типов добавляем базовый тип
        if (!string.IsNullOrEmpty(baseTypeName) && 
            char.IsUpper(baseTypeName[0]) && 
            !string.Equals(baseTypeName, currentModelName, StringComparison.OrdinalIgnoreCase))
        {
            usedTypes.Add(baseTypeName);
        }
        
        // Извлекаем generic аргументы
        var genericArgs = typeString.Substring(genericStart + 1);
        genericArgs = genericArgs.TrimEnd('>');
        var argTypes = SplitGenericArguments(genericArgs);
        foreach (var argType in argTypes)
        {
            var cleanType = argType.Trim().Split('<')[0].Split('[')[0].Trim();
            if (!string.IsNullOrEmpty(cleanType) && 
                char.IsUpper(cleanType[0]) && 
                !string.Equals(cleanType, currentModelName, StringComparison.OrdinalIgnoreCase) &&
                !builtInTypes.Contains(cleanType))
            {
                usedTypes.Add(cleanType);
            }
        }
    }
    
    /// <summary>
    /// Разделяет generic аргументы, учитывая вложенные generic
    /// </summary>
    private List<string> SplitGenericArguments(string genericArgs)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        
        foreach (var ch in genericArgs)
        {
            if (ch == '<')
            {
                depth++;
                current.Append(ch);
            }
            else if (ch == '>')
            {
                depth--;
                current.Append(ch);
            }
            else if (ch == ',' && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        
        return result;
    }

    private Dictionary<string, string> GenerateClients(IReadOnlyList<ApiOperation> operations, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>();

        var groupedOperations = GroupOperations(operations, config);

        foreach (var group in groupedOperations)
        {
            var clientCode = GenerateClientCode(group.Value, group.Key, document, config, schemaResolver, options);
            
            // Определяем расширение файла в зависимости от HTTP клиента
            var fileExtension = options.HttpClientLibrary == HttpClientLibrary.Axios ? ".axios.ts" : ".fetch.ts";
            var fileName = $"{_namingConverter.ToCamelCase(group.Key)}{fileExtension}";
            
            string filePath;
            if (config.FileOrganization.SeparateClients)
            {
                var clientsPath = config.FileOrganization.ClientsPath ?? "api";
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
            groups["Api"] = operations.ToList();
                break;
        }

        return groups;
    }

    private string ExtractRouteGroup(string path)
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

        var sanitized = _namingConverter.SanitizeIdentifier(lastSegment);
        return _namingConverter.ToPascalCase(sanitized);
    }

    private string GenerateClientCode(List<ApiOperation> operations, string clientName, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var operationTemplates = operations.Select(op => CreateTypeScriptOperationTemplateData(op, document, config, schemaResolver, options)).ToList();

        // Собираем используемые типы для импортов
        var importedTypes = CollectUsedTypes(operationTemplates);
        
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
        
        var templateData = new TypeScriptClientTemplateData
        {
            ClientName = clientName,
            Operations = operationTemplates,
            HttpClientLibrary = options.HttpClientLibrary,
            ImportedTypes = importedTypes,
            ModelsPath = modelsImportPath,
            AxiosOptions = options.Axios
        };

        // Выбираем шаблон в зависимости от HTTP клиента
        var templateName = options.HttpClientLibrary == HttpClientLibrary.Axios
            ? "axios-client.cshtml"
            : "typescript-client.cshtml";

        return _templateRegistry.Render(templateName, templateData);
    }

    /// <summary>
    /// Собирает все используемые типы из операций для генерации импортов
    /// </summary>
    private List<string> CollectUsedTypes(IReadOnlyList<TypeScriptOperationTemplateData> operations)
    {
        var types = new HashSet<string>();
        
        foreach (var operation in operations)
        {
            // Собираем типы из return type
            CollectTypesFromTypeInfo(operation.ReturnTypeInfo, types);
            
            // Собираем типы из request body
            if (operation.RequestBodyTypeInfo != null)
            {
                CollectTypesFromTypeInfo(operation.RequestBodyTypeInfo, types);
            }
            
            // Собираем типы из параметров
            foreach (var param in operation.PathParameters)
            {
                CollectTypesFromTypeInfo(param.TypeInfo, types);
            }
            
            foreach (var param in operation.QueryParameters)
            {
                CollectTypesFromTypeInfo(param.TypeInfo, types);
            }
            
            foreach (var param in operation.HeaderParameters)
            {
                CollectTypesFromTypeInfo(param.TypeInfo, types);
            }
            
            // Собираем типы из form data properties
            if (operation.FormDataProperties != null)
            {
                foreach (var formProp in operation.FormDataProperties)
                {
                    CollectTypesFromTypeInfo(formProp.TypeInfo, types);
                }
            }
        }
        
        return types.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Рекурсивно собирает типы из TypeInfo
    /// </summary>
    private void CollectTypesFromTypeInfo(TypeInfo typeInfo, HashSet<string> types)
    {
        if (typeInfo == null)
            return;
        
        // Пропускаем примитивные типы и встроенные типы
        if (typeInfo.IsPrimitive || 
            typeInfo.IsVoid ||
            typeInfo.IsFile ||
            string.IsNullOrEmpty(typeInfo.BaseType))
        {
            // Но все равно проверяем generic аргументы
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectTypesFromTypeInfo(genericArg, types);
            }
            return;
        }
        
        var baseType = typeInfo.BaseType;
        
        // Обрабатываем массивы (TypeScript формат: Type[])
        if (baseType.Contains("[]"))
        {
            // Извлекаем тип элемента массива
            var elementType = baseType.Replace("[]", "").Trim();
            if (!string.IsNullOrEmpty(elementType) && 
                elementType != "any" && 
                elementType != "unknown" && 
                elementType != "object")
            {
                // Рекурсивно обрабатываем тип элемента
                var elementTypeInfo = TypeInfo.Simple(elementType);
                CollectTypesFromTypeInfo(elementTypeInfo, types);
            }
            return;
        }
        
        // Обрабатываем коллекции через IsCollection
        if (typeInfo.IsCollection && typeInfo.GenericArguments.Count > 0)
        {
            // Обрабатываем generic аргументы (элементы коллекции)
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectTypesFromTypeInfo(genericArg, types);
            }
            return;
        }
        
        // Обрабатываем словари через IsDictionary
        if (typeInfo.IsDictionary && typeInfo.GenericArguments.Count >= 2)
        {
            // Обрабатываем generic аргументы (ключ и значение)
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectTypesFromTypeInfo(genericArg, types);
            }
            return;
        }
        
        // Пропускаем встроенные типы TypeScript
        if (baseType == "any" || 
            baseType == "unknown" || 
            baseType == "object" ||
            baseType == "Record" ||
            baseType == "Array" ||
            baseType == "Promise" ||
            baseType == "Response" ||
            baseType.StartsWith("Record<"))
        {
            // Но все равно проверяем generic аргументы
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectTypesFromTypeInfo(genericArg, types);
            }
            return;
        }
        
        // Извлекаем имя типа без generic параметров и массивов
        var typeName = baseType.Split('<')[0].Split('[')[0].Trim();
        
        // Если это не встроенный тип, добавляем его
        if (!string.IsNullOrEmpty(typeName) && 
            typeName != "any" && 
            typeName != "unknown" && 
            typeName != "object" &&
            !typeName.Contains("Record") &&
            !typeName.Contains("Array") &&
            !typeName.Contains("Promise"))
        {
            types.Add(typeName);
        }
        
        // Рекурсивно обрабатываем generic аргументы
        foreach (var genericArg in typeInfo.GenericArguments)
        {
            CollectTypesFromTypeInfo(genericArg, types);
        }
    }

    private TypeScriptOperationTemplateData CreateTypeScriptOperationTemplateData(ApiOperation operation, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var operationId = string.IsNullOrWhiteSpace(operation.OperationId) 
            ? GenerateOperationId(operation.Path, operation.HttpMethod) 
            : operation.OperationId;
        var methodName = _namingConverter.ToCamelCase(operationId);
        
        if (string.IsNullOrWhiteSpace(methodName))
        {
            methodName = "execute";
        }
        
        var httpMethod = operation.HttpMethod.Method.ToUpperInvariant();
        var path = operation.Path ?? string.Empty;

        var pathParameters = new List<TypeScriptParameterTemplateData>();
        var queryParameters = new List<TypeScriptParameterTemplateData>();
        var headerParameters = new List<TypeScriptParameterTemplateData>();
        var seenVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateParams = new List<string>();

        foreach (var parameter in operation.Parameters)
        {
            var paramName = _namingConverter.ToCamelCase(parameter.Name);
            
            // Проверяем на дубликаты по VariableName
            if (seenVariableNames.Contains(paramName))
            {
                duplicateParams.Add(paramName);
                _logger?.LogWarning(
                    "Duplicate parameter variable name '{VariableName}' found in operation '{OperationId}' at path '{Path}'. " +
                    "Parameter will be deduplicated.",
                    paramName, operation.OperationId ?? "unknown", operation.Path ?? "unknown");
                continue;
            }
            seenVariableNames.Add(paramName);
            
            TypeInfo paramTypeInfo;
            var originalOperation = FindOriginalOperation(document, operation);
            if (originalOperation != null && originalOperation.Parameters != null)
            {
                var openApiParam = originalOperation.Parameters.FirstOrDefault(p => p.Name == parameter.Name);
                if (openApiParam?.Schema != null)
                {
                    paramTypeInfo = _typescriptTypeMapper.MapPropertyTypeInfo(openApiParam.Schema, parameter.IsRequired, schemaResolver);
                }
                else
                {
                    paramTypeInfo = TypeInfo.Simple(parameter.Type ?? "string");
                }
            }
            else
            {
                paramTypeInfo = TypeInfo.Simple(parameter.Type ?? "string");
            }

            var paramTemplate = new TypeScriptParameterTemplateData
            {
                Name = parameter.Name,
                VariableName = paramName,
                TypeInfo = paramTypeInfo,
                Type = paramTypeInfo.FullTypeName,
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

        TypeInfo? requestBodyTypeInfo = null;
        var requestBodyContentType = "application/json";
        var isFormData = false;
        var formDataProperties = new List<TypeScriptFormDataPropertyTemplateData>();
        
        if (operation.RequestBody != null)
        {
            requestBodyContentType = operation.RequestBody.ContentType ?? "application/json";
            isFormData = operation.RequestBody.IsFormData;
            
            if (isFormData && operation.RequestBody.FormDataProperties != null)
            {
                foreach (var formDataPropEntry in operation.RequestBody.FormDataProperties)
                {
                    var formDataProp = formDataPropEntry.Value;
                    var propName = _namingConverter.ToCamelCase(formDataProp.Name);
                    
                    TypeInfo typeInfo = TypeInfo.Simple("string", isPrimitive: true);
                    var originalOperation = FindOriginalOperation(document, operation);
                    if (originalOperation?.RequestBody?.Content != null)
                    {
                        var content = originalOperation.RequestBody.Content.FirstOrDefault();
                        if (content.Key != null && content.Value?.Schema?.Properties != null)
                        {
                            var propertySchema = content.Value.Schema.Properties.FirstOrDefault(p => p.Key == formDataProp.Name).Value;
                            if (propertySchema != null)
                            {
                                typeInfo = _typescriptTypeMapper.MapPropertyTypeInfo(propertySchema, formDataProp.IsRequired, schemaResolver);
                            }
                        }
                    }
                    
                    formDataProperties.Add(new TypeScriptFormDataPropertyTemplateData
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
                var originalOperation = FindOriginalOperation(document, operation);
                if (originalOperation?.RequestBody?.Content != null)
                {
                    var contentTypes = new[] { "application/json", "application/xml", "text/json", "text/xml" };
                    var requestBodySchema = FindSchemaInContent(originalOperation.RequestBody.Content, contentTypes);
                    
                    if (requestBodySchema != null)
                    {
                        requestBodyTypeInfo = _typescriptTypeMapper.MapTypeInfo(requestBodySchema, schemaResolver);
                    }
                }
                
                if (requestBodyTypeInfo == null)
                {
                    requestBodyTypeInfo = TypeInfo.Simple(operation.RequestBody.Type ?? "any");
                }
            }
        }

        var returnTypeInfo = DetermineReturnTypeInfo(operation, document, schemaResolver);

        return new TypeScriptOperationTemplateData
        {
            MethodName = methodName,
            HttpMethod = httpMethod,
            Path = path,
            Summary = DecodeHtmlEntities(operation.Summary),
            Description = DecodeHtmlEntities(operation.Description),
            Parameters = "", // Будет сгенерировано в шаблоне
            ReturnTypeInfo = returnTypeInfo,
            ReturnType = returnTypeInfo.IsTask && returnTypeInfo.GenericArguments.Count > 0 
                ? returnTypeInfo.GenericArguments[0].FullTypeName 
                : returnTypeInfo.FullTypeName,
            HasRequestBody = operation.RequestBody != null,
            RequestBodyParameter = "request",
            RequestBodyTypeInfo = requestBodyTypeInfo,
            RequestBodyContentType = requestBodyContentType,
            PathParameters = pathParameters,
            QueryParameters = queryParameters,
            HeaderParameters = headerParameters,
            IsFormData = isFormData,
            FormDataProperties = formDataProperties.Count > 0 ? formDataProperties : null
        };
    }

    private TypeInfo DetermineReturnTypeInfo(ApiOperation operation, OpenApiDocument document, ISchemaResolver schemaResolver)
    {
        var successResponseKeys = new[] { "200", "201", "202", "204", "default" };
        var successResponse = operation.Responses.FirstOrDefault(r => successResponseKeys.Contains(r.Key));

        if (successResponse.Key == null)
        {
            successResponse = operation.Responses.FirstOrDefault();
        }

        if (successResponse.Key == null)
        {
            return TypeInfo.Simple("Response");
        }

        var originalOperation = FindOriginalOperation(document, operation);
        if (originalOperation != null && originalOperation.Responses != null)
        {
            var openApiResponse = originalOperation.Responses.FirstOrDefault(r => r.Key == successResponse.Key);
            
            if (openApiResponse.Key != null && openApiResponse.Value?.Content != null)
            {
                var contentTypes = new[] { "application/json", "application/xml", "text/json", "text/xml" };
                var responseSchema = FindSchemaInContent(openApiResponse.Value.Content, contentTypes);
                
                if (responseSchema != null)
                {
                    try
                    {
                        var typeInfo = _typescriptTypeMapper.MapTypeInfo(responseSchema, schemaResolver);
                        return typeInfo;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to map response type for operation '{OperationId}'", operation.OperationId);
                    }
                }
            }
        }

        var responseType = successResponse.Value.Type;
        if (string.IsNullOrEmpty(responseType))
        {
            return TypeInfo.Simple("Response");
        }

        if (responseType == "void" || responseType == "null")
        {
            return TypeInfo.Void();
        }

        return TypeInfo.Simple(responseType);
    }

    private IOpenApiSchema? FindSchemaInContent(IDictionary<string, IOpenApiMediaType> content, string[] preferredContentTypes)
    {
        foreach (var contentType in preferredContentTypes)
        {
            if (content.TryGetValue(contentType, out var mediaType) && mediaType?.Schema != null)
            {
                return mediaType.Schema;
            }
        }
        
        var firstContent = content.FirstOrDefault();
        if (firstContent.Key != null && firstContent.Value?.Schema != null)
        {
            return firstContent.Value.Schema;
        }
        
        return null;
    }

    private void ValidateOperations(IReadOnlyList<ApiOperation> operations, GenerationResult result)
    {
        var operationIds = new Dictionary<string, List<string>>();
        var warnings = result.Warnings.ToList();
        
        foreach (var operation in operations)
        {
            var operationId = string.IsNullOrWhiteSpace(operation.OperationId) 
                ? GenerateOperationId(operation.Path, operation.HttpMethod) 
                : operation.OperationId;
            
            if (string.IsNullOrWhiteSpace(operationId))
            {
                var warning = $"Operation at path '{operation.Path}' with method '{operation.HttpMethod.Method}' has empty operationId";
                warnings.Add(warning);
                _logger.LogWarning(warning);
                continue;
            }
            
            var key = $"{operationId}";
            if (!operationIds.ContainsKey(key))
            {
                operationIds[key] = new List<string>();
            }
            operationIds[key].Add($"{operation.HttpMethod.Method} {operation.Path}");
        }
        
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

    private void ValidateModels(IReadOnlyList<ApiModel> models, GenerationResult result)
    {
        var modelNames = new HashSet<string>();
        var warnings = result.Warnings.ToList();
        
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                var warning = "Found model with empty name, skipping";
                warnings.Add(warning);
                _logger.LogWarning(warning);
                continue;
            }
            
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

    private async Task<Dictionary<string, string>> GenerateZodSchemasAsync(IReadOnlyList<ApiModel> models, IReadOnlyList<ApiEnum> enums, GenerationConfiguration config, OpenApiDocument document, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>();
        
        // Создаем Zod генератор
        // Используем полное имя типа, чтобы избежать циклической зависимости
        var zodAssembly = Assembly.Load("ValIO.Generators.TypeScript.Zod");
        var zodTypeMapperType = zodAssembly.GetType("ValIO.Generators.TypeScript.Zod.ZodTypeMapper");
        var zodSchemaGeneratorType = zodAssembly.GetType("ValIO.Generators.TypeScript.Zod.ZodSchemaGenerator");
        
        if (zodTypeMapperType == null || zodSchemaGeneratorType == null)
        {
            throw new InvalidOperationException("Zod types not found. Make sure ValIO.Generators.TypeScript.Zod assembly is loaded.");
        }
        
        var zodTypeMapper = Activator.CreateInstance(zodTypeMapperType, _namingConverter);
        
        // Создаем отдельный ITemplateRegistry для Zod шаблонов
        ITemplateRegistry zodTemplateRegistry;
        if (_templateRegistryFactory != null)
        {
            zodTemplateRegistry = _templateRegistryFactory.CreateRegistry(zodAssembly);
            // Предзагружаем шаблоны Zod
            await zodTemplateRegistry.PreloadTemplatesAsync();
        }
        else
        {
            // Fallback: используем тот же реестр (может не работать, если шаблоны в другой сборке)
            zodTemplateRegistry = _templateRegistry;
        }
        
        var zodSchemaGenerator = Activator.CreateInstance(zodSchemaGeneratorType,
            _namingConverter,
            _typescriptTypeMapper,
            zodTypeMapper,
            zodTemplateRegistry,
            _logger) as dynamic;
        
        if (zodSchemaGenerator == null)
        {
            throw new InvalidOperationException("Failed to create ZodSchemaGenerator instance.");
        }

        // Генерируем Zod схемы для моделей
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                continue;
            }
            
            var schemaCode = (string)zodSchemaGenerator.GenerateSchema(model, schemaResolver);
            var fileName = $"{_namingConverter.ToCamelCase(model.Name)}.schema.ts";
            
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "models";
                filePath = Path.Combine(config.OutputPath, modelsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = schemaCode;
        }

        // Генерируем Zod схемы для enum'ов
        foreach (var apiEnum in enums)
        {
            if (string.IsNullOrWhiteSpace(apiEnum.Name))
            {
                continue;
            }
            
            var schemaCode = (string)zodSchemaGenerator.GenerateEnumSchema(apiEnum);
            var fileName = $"{_namingConverter.ToCamelCase(apiEnum.Name)}.schema.ts";
            
            string filePath;
            if (config.FileOrganization.SeparateModels)
            {
                var modelsPath = config.FileOrganization.ModelsPath ?? "models";
                filePath = Path.Combine(config.OutputPath, modelsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = schemaCode;
        }

        return files;
    }

    private async Task<Dictionary<string, string>> GenerateTanStackQueryHooksAsync(IReadOnlyList<ApiOperation> operations, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options)
    {
        var files = new Dictionary<string, string>();

        // Создаем TanStack Query генератор
        // Используем полное имя типа, чтобы избежать циклической зависимости
        var tanStackQueryAssembly = Assembly.Load("ValIO.Generators.TypeScript.TanStackQuery");
        var tanStackQueryGeneratorType = tanStackQueryAssembly.GetType("ValIO.Generators.TypeScript.TanStackQuery.TanStackQueryGenerator");
        if (tanStackQueryGeneratorType == null)
        {
            throw new InvalidOperationException("TanStackQueryGenerator type not found. Make sure ValIO.Generators.TypeScript.TanStackQuery assembly is loaded.");
        }
        
        var queryTemplateRegistry = _templateRegistryFactory != null
            ? _templateRegistryFactory.CreateRegistry(tanStackQueryAssembly)
            : _templateRegistry;
        
        // Предзагружаем шаблоны TanStack Query
        await queryTemplateRegistry.PreloadTemplatesAsync();

        // Создаем logger для TanStackQueryGenerator через reflection
        // Используем NullLogger, так как logger опционален
        var nullLoggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(tanStackQueryGeneratorType);
        var logger = Activator.CreateInstance(nullLoggerType);
        
        // Получаем конструктор с правильными типами параметров
        var constructor = tanStackQueryGeneratorType.GetConstructor(new[]
        {
            typeof(INamingConverter),
            typeof(TypeScriptTypeMapper),
            typeof(ITemplateRegistry),
            typeof(ILogger<>).MakeGenericType(tanStackQueryGeneratorType)
        });
        
        if (constructor == null)
        {
            throw new InvalidOperationException($"Constructor not found for TanStackQueryGenerator. Expected: (INamingConverter, TypeScriptTypeMapper, ITemplateRegistry, ILogger<{tanStackQueryGeneratorType.Name}>)");
        }
        
        var queryGenerator = constructor.Invoke(new object[]
        {
            _namingConverter,
            _typescriptTypeMapper,
            queryTemplateRegistry,
            logger!
        }) as dynamic;
        
        if (queryGenerator == null)
        {
            throw new InvalidOperationException("Failed to create TanStackQueryGenerator instance.");
        }

        // Группируем операции
        var groupedOperations = GroupOperations(operations, config);

        foreach (var group in groupedOperations)
        {
            var hooksCode = GenerateTanStackQueryHooksCode(group.Value, group.Key, document, config, schemaResolver, options, queryGenerator);
            var fileName = $"{_namingConverter.ToCamelCase(group.Key)}.queries.ts";
            
            string filePath;
            if (config.FileOrganization.SeparateClients)
            {
                var clientsPath = config.FileOrganization.ClientsPath ?? "api";
                filePath = Path.Combine(config.OutputPath, clientsPath, fileName);
            }
            else
            {
                filePath = Path.Combine(config.OutputPath, fileName);
            }

            files[filePath] = hooksCode;
        }

        return files;
    }

    private string GenerateTanStackQueryHooksCode(List<ApiOperation> operations, string groupName, OpenApiDocument document, GenerationConfiguration config, ISchemaResolver schemaResolver, TypeScriptGenerationOptions options, dynamic queryGenerator)
    {
        var hooks = new List<string>();
        var allOperationTemplates = new List<TypeScriptOperationTemplateData>();
        
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
        
        // Вычисляем путь к базовым клиентам для импортов
        // Базовые клиенты находятся в той же папке, что и queries файлы
        var clientFileExtension = options.HttpClientLibrary == HttpClientLibrary.Axios ? ".axios" : ".fetch";
        var clientFileName = $"{_namingConverter.ToCamelCase(groupName)}{clientFileExtension}";
        var clientImportPath = $"./{clientFileName}";

        // Сначала создаем все operation templates для сбора типов
        foreach (var operation in operations)
        {
            var operationTemplate = CreateTypeScriptOperationTemplateData(operation, document, config, schemaResolver, options);
            allOperationTemplates.Add(operationTemplate);
        }

        // Собираем все используемые типы из всех операций
        var importedTypes = CollectUsedTypes(allOperationTemplates);

        // Собираем все уникальные схемы для валидации
        var requestSchemas = new HashSet<(string Name, string FileName)>();
        var responseSchemas = new HashSet<(string Name, string FileName)>();
        
        if (options.ModelFormat == ModelFormat.ValidationSchemas && 
            options.ValidationLibrary == ValidationLibrary.Zod)
        {
            foreach (var operationTemplate in allOperationTemplates)
            {
                // Собираем request схемы
                if (options.SerializeWithSchemas && 
                    operationTemplate.HasRequestBody && 
                    operationTemplate.RequestBodyTypeInfo != null)
                {
                    var requestTypeName = operationTemplate.RequestBodyTypeInfo.FullTypeName;
                    var cleanTypeName = requestTypeName.Split('<')[0].Split('[')[0].Trim();
                    var schemaName = $"{_namingConverter.ToPascalCase(cleanTypeName)}Schema";
                    var schemaFileName = _namingConverter.ToCamelCase(cleanTypeName);
                    requestSchemas.Add((schemaName, schemaFileName));
                }
                
                // Собираем response схемы
                if (options.DeserializeWithSchemas)
                {
                    var responseTypeName = operationTemplate.ReturnTypeInfo.IsTask && 
                        operationTemplate.ReturnTypeInfo.GenericArguments.Count > 0
                        ? operationTemplate.ReturnTypeInfo.GenericArguments[0].FullTypeName
                        : operationTemplate.ReturnTypeInfo.FullTypeName;
                    
                    if (!string.IsNullOrEmpty(responseTypeName) && responseTypeName != "void")
                    {
                        var cleanTypeName = responseTypeName.Split('<')[0].Split('[')[0].Trim();
                        var schemaName = $"{_namingConverter.ToPascalCase(cleanTypeName)}Schema";
                        var schemaFileName = _namingConverter.ToCamelCase(cleanTypeName);
                        responseSchemas.Add((schemaName, schemaFileName));
                    }
                }
            }
        }

        // Генерируем импорты один раз в начале файла
        var imports = new List<string>();
        
        // Типы для экспорта после импортов
        var typeExports = new List<string>();
        
        // Создаем словарь для связи methodName -> operationTemplate для получения типов ответа
        var methodToTemplateMap = new Dictionary<string, TypeScriptOperationTemplateData>();
        
        // Импорт базовых методов из клиентских файлов
        var methodNames = new List<string>();
        for (int i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            var operationTemplate = allOperationTemplates[i];
            var operationId = string.IsNullOrWhiteSpace(operation.OperationId)
                ? GenerateOperationId(operation.Path, operation.HttpMethod)
                : operation.OperationId;
            var methodName = _namingConverter.ToCamelCase(operationId);
            if (!string.IsNullOrWhiteSpace(methodName) && !methodNames.Contains(methodName))
            {
                methodNames.Add(methodName);
                methodToTemplateMap[methodName] = operationTemplate;
            }
        }
        
        if (methodNames.Count > 0)
        {
            var methodNamesStr = string.Join(", ", methodNames);
            imports.Add($"import {{ {methodNamesStr} }} from '{clientImportPath}';");
        }
        
        // Импорт TanStack Query
        var hasQuery = operations.Any(o => o.HttpMethod.Method.Equals("GET", StringComparison.OrdinalIgnoreCase));
        var hasMutation = operations.Any(o => !o.HttpMethod.Method.Equals("GET", StringComparison.OrdinalIgnoreCase));
        
        // Собираем все необходимые типы для TanStack Query
        var tanStackImports = new List<string>();
        
        if (hasQuery)
        {
            tanStackImports.AddRange(new[] { "useQuery", "UseQueryOptions", "UseQueryResult", "QueryFunction", "QueryKey", "QueryClient", "useInfiniteQuery", "UseInfiniteQueryOptions", "UseInfiniteQueryResult", "InfiniteData" });
        }
        
        if (hasMutation)
        {
            tanStackImports.AddRange(new[] { "useMutation", "UseMutationOptions", "UseMutationResult", "MutationFunction" });
            // QueryClient уже добавлен выше, если есть queries, но нам он нужен и для mutations
            if (!hasQuery)
            {
                tanStackImports.Add("QueryClient");
            }
        }
        
        // Импорт AxiosInstance если используется Axios
        if (options.HttpClientLibrary == HttpClientLibrary.Axios && (hasQuery || hasMutation))
        {
            imports.Insert(0, "import { AxiosInstance } from 'axios';");
        }
        
        // Удаляем дубликаты и сортируем для консистентности
        var uniqueImports = tanStackImports.Distinct().OrderBy(x => x).ToList();
        
        if (uniqueImports.Count > 0)
        {
            var importsStr = string.Join(", ", uniqueImports);
            imports.Add($"import {{ {importsStr} }} from '@tanstack/react-query';");
        }
        
        // Импорт типов моделей
        if (importedTypes.Count > 0 && modelsImportPath != null)
        {
            foreach (var typeName in importedTypes)
            {
                var fileName = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
                imports.Add($"import type {{ {typeName} }} from '{modelsImportPath}/{fileName}.model';");
            }
        }
        
        // Импорт Zod и схем
        if (options.ModelFormat == ModelFormat.ValidationSchemas && 
            options.ValidationLibrary == ValidationLibrary.Zod && 
            (requestSchemas.Count > 0 || responseSchemas.Count > 0))
        {
            imports.Add("import { z } from 'zod';");
            
            // Объединяем request и response схемы, чтобы избежать дублирования
            var allSchemas = new HashSet<(string Name, string FileName)>();
            allSchemas.UnionWith(requestSchemas);
            allSchemas.UnionWith(responseSchemas);
            
            foreach (var (schemaName, schemaFileName) in allSchemas)
            {
                if (modelsImportPath != null)
                {
                    imports.Add($"import {{ {schemaName} }} from '{modelsImportPath}/{schemaFileName}.schema';");
                }
                else
                {
                    imports.Add($"import {{ {schemaName} }} from '../models/{schemaFileName}.schema';");
                }
            }
        }

        // Генерируем хуки без импортов
        for (int i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            var operationTemplate = allOperationTemplates[i];
            
            var operationId = string.IsNullOrWhiteSpace(operation.OperationId)
                ? GenerateOperationId(operation.Path, operation.HttpMethod)
                : operation.OperationId;
            var clientMethodName = _namingConverter.ToCamelCase(operationId);
            
            var isGet = operation.HttpMethod.Method.Equals("GET", StringComparison.OrdinalIgnoreCase);
            
            if (isGet)
            {
                var hookCode = queryGenerator.GenerateQueryHook(operation, operationTemplate, options, schemaResolver, importedTypes, modelsImportPath, clientMethodName, clientImportPath);
                hooks.Add(hookCode);
            }
            else
            {
                var hookCode = queryGenerator.GenerateMutationHook(operation, operationTemplate, options, schemaResolver, importedTypes, modelsImportPath, clientMethodName, clientImportPath);
                hooks.Add(hookCode);
            }
        }

        // Генерируем type exports после всех импортов
        foreach (var methodName in methodNames)
        {
            if (methodToTemplateMap.TryGetValue(methodName, out var operationTemplate))
            {
                var returnTypeName = char.ToUpperInvariant(methodName[0]) + methodName.Substring(1) + "ReturnType";
                var awaitedReturnTypeName = char.ToUpperInvariant(methodName[0]) + methodName.Substring(1) + "AwaitedReturnType";
                
                // Получаем тип ответа из operationTemplate.ReturnType (это уже развернутый тип без Promise)
                var responseType = operationTemplate.ReturnType;
                
                // Генерируем ReturnType как Promise<ResponseType>
                typeExports.Add($"export type {returnTypeName} = Promise<{responseType}>;");
                
                // Генерируем AwaitedReturnType как Awaited<ReturnType>
                typeExports.Add($"export type {awaitedReturnTypeName} = Awaited<{returnTypeName}>;");
            }
        }
        
        // Объединяем импорты, type exports и хуки
        var importsCode = string.Join("\n", imports);
        var typeExportsCode = typeExports.Count > 0 ? "\n" + string.Join("\n", typeExports) : "";
        var hooksCode = string.Join("\n\n", hooks);
        
        if (!string.IsNullOrEmpty(importsCode))
        {
            return importsCode + typeExportsCode + "\n\n" + hooksCode;
        }
        
        if (!string.IsNullOrEmpty(typeExportsCode))
        {
            return typeExportsCode.TrimStart() + "\n\n" + hooksCode;
        }
        
        return hooksCode;
    }

    private async Task EnsureTemplatesPreloadedAsync()
    {
        if (_templatesPreloaded)
            return;

        try
        {
            if (!_templateRegistry.HasTemplate("typescript-model.cshtml") || 
                !_templateRegistry.HasTemplate("typescript-client.cshtml") ||
                !_templateRegistry.HasTemplate("typescript-enum.cshtml") ||
                !_templateRegistry.HasTemplate("axios-client.cshtml"))
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
