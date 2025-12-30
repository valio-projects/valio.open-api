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
            // Предзагружаем шаблоны при первом использовании
            if (!_templatesPreloaded)
            {
                await EnsureTemplatesPreloadedAsync();
            }

            _logger.LogInformation("Starting C# code generation");

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

            // Извлекаем операции
            var operations = operationExtractor.ExtractOperations(document);
            _logger.LogInformation("Extracted {Count} operations", operations.Count);
            
            if (config.GenerateClients && operations.Count == 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.Add("No operations found in OpenAPI schema");
                result.Warnings = warnings;
            }

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
                    var warningMsg = $"Failed to map types for operation '{operation.OperationId}': {ex.Message}";
                    mappingWarnings.Add(warningMsg);
                    _logger.LogWarning(ex, "Failed to map types for operation: {OperationId}", operation.OperationId);
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
                    generationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to generate enums");
                }
            }

            if (config.GenerateModels)
            {
                try
                {
                    var modelFiles = GenerateModels(models, config);
                    foreach (var file in modelFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to generate models: {ex.Message}";
                    generationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to generate models");
                }
            }

            if (config.GenerateClients)
            {
                try
                {
                    var clientFiles = GenerateClients(operations, document, config);
                    foreach (var file in clientFiles)
                    {
                        generatedFiles[file.Key] = file.Value;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to generate clients: {ex.Message}";
                    generationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to generate clients");
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
            _logger.LogError(ex, "Error during C# code generation");
            result.Errors = new List<string> { ex.Message };
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

    private OpenApiOperation? FindOriginalOperation(OpenApiDocument document, ApiOperation operation)
    {
        if (document.Paths == null)
            return null;

        foreach (var pathEntry in document.Paths)
        {
            if (pathEntry.Value.Operations == null)
                continue;

            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var openApiOperation = operationEntry.Value;
                var operationId = openApiOperation.OperationId ?? 
                    $"{operationEntry.Key.Method.ToLower()}{pathEntry.Key.Replace("/", "").Replace("{", "").Replace("}", "")}";

                if (operationId == operation.OperationId && 
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

    private Dictionary<string, string> GenerateModels(IReadOnlyList<ApiModel> models, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        foreach (var model in models)
        {
            var modelCode = GenerateModelCode(model, config);
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

    private string GenerateModelCode(ApiModel model, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = _namingConverter.ToPascalCase(model.Name);

        // Получаем C# специфичные опции из конфигурации
        var csharpOptions = GetCSharpOptions(config);

        var properties = model.Properties.Select(p => new PropertyTemplateData
        {
            Name = _namingConverter.ToPascalCase(p.Name),
            Type = p.Type,
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
        }).ToList();

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
            NeedsRegularExpressions = needsRegularExpressions
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

    private Dictionary<string, string> GenerateClients(IReadOnlyList<ApiOperation> operations, OpenApiDocument document, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        // Группируем операции по тегам или создаем один клиент
        var groupedOperations = GroupOperations(operations, config);

        foreach (var group in groupedOperations)
        {
            var clientCode = GenerateClientCode(group.Value, group.Key, document, config);
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

    private string GenerateClientCode(List<ApiOperation> operations, string clientName, OpenApiDocument document, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = $"{_namingConverter.ToPascalCase(clientName)}Client";

        var operationTemplates = operations.Select(op => CreateOperationTemplateData(op, document, config)).ToList();

        var templateData = new ClientTemplateData
        {
            Namespace = namespaceName,
            ClassName = className,
            ClientName = clientName,
            Operations = operationTemplates
        };

        return _templateRegistry.Render("csharp-client.cshtml", templateData);
    }

    private OperationTemplateData CreateOperationTemplateData(ApiOperation operation, OpenApiDocument document, GenerationConfiguration config)
    {
        var methodName = _namingConverter.ToPascalCase(operation.OperationId);
        var httpMethod = operation.HttpMethod.Method.ToUpperInvariant();
        var path = operation.Path;

        // Разделяем параметры по типам
        var pathParameters = new List<ParameterTemplateData>();
        var queryParameters = new List<ParameterTemplateData>();
        var headerParameters = new List<ParameterTemplateData>();
        var allParameters = new List<string>();

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
                if (originalOperation != null)
                {
                    var openApiParam = originalOperation.Parameters
                        .FirstOrDefault(p => p.Name == parameter.Name);
                    if (openApiParam?.Schema != null)
                    {
                        var schemaResolver = new SchemaResolver(document);
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
            
            var paramString = FormatParameterString(paramTypeInfo, paramName, parameter.IsRequired);
            allParameters.Add(paramString);

            var paramTemplate = new ParameterTemplateData
            {
                Name = parameter.Name,
                VariableName = paramName,
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
        var requestBodyType = "object";
        var requestBodyContentType = "application/json";
        var isFormData = false;
        var formDataProperties = new List<FormDataPropertyTemplateData>();
        
        if (operation.RequestBody != null)
        {
            requestBodyType = operation.RequestBody.Type;
            if (string.IsNullOrEmpty(requestBodyType) || requestBodyType == "object")
            {
                requestBodyType = "object";
            }
            
            requestBodyContentType = operation.RequestBody.ContentType ?? "application/json";
            isFormData = operation.RequestBody.IsFormData;
            
            // Если это form-data, создаем параметры для каждого свойства
            if (isFormData && operation.RequestBody.FormDataProperties != null)
            {
                // Нужен SchemaResolver для маппинга типов
                var formDataSchemaResolver = new SchemaResolver(document);
                
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
                                    typeInfo = _csharpTypeMapper.MapFormDataPropertyTypeInfo(propertySchema, formDataProp.IsRequired, formDataSchemaResolver);
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
                    
                    // Формируем строку параметра на основе TypeInfo
                    var paramString = FormatParameterString(typeInfo, propName, formDataProp.IsRequired);
                    allParameters.Add(paramString);
                    
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
                allParameters.Add($"{requestBodyType} {requestBodyParameter}");
            }
        }

        // Определяем тип ответа на основе успешных ответов (2xx) через TypeInfo
        var returnTypeInfo = DetermineReturnTypeInfo(operation, document, config);
        
        // Определяем, есть ли тип ответа (не void, не просто Task, не HttpResponseMessage)
        var hasResponseType = returnTypeInfo.IsTask && returnTypeInfo.GenericArguments.Count > 0 &&
                             !returnTypeInfo.GenericArguments[0].IsVoid &&
                             !returnTypeInfo.GenericArguments[0].IsHttpResponseMessage;

        return new OperationTemplateData
        {
            MethodName = methodName,
            HttpMethod = httpMethod,
            Path = path,
            Summary = DecodeHtmlEntities(operation.Summary),
            Parameters = string.Join(", ", allParameters),
            ReturnTypeInfo = returnTypeInfo,
            HasRequestBody = operation.RequestBody != null,
            RequestBodyParameter = requestBodyParameter,
            RequestBodyContentType = requestBodyContentType,
            HasResponseType = hasResponseType,
            HasPathParameters = pathParameters.Count > 0,
            HasQueryParameters = queryParameters.Count > 0,
            HasHeaderParameters = headerParameters.Count > 0,
            PathParameters = pathParameters,
            QueryParameters = queryParameters,
            HeaderParameters = headerParameters,
            IsFormData = isFormData,
            FormDataProperties = formDataProperties.Count > 0 ? formDataProperties : null
        };
    }

    /// <summary>
    /// Определяет тип возврата операции через TypeInfo
    /// </summary>
    private TypeInfo DetermineReturnTypeInfo(ApiOperation operation, OpenApiDocument document, GenerationConfiguration config)
    {
        var schemaResolver = new SchemaResolver(document);
        
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
        if (originalOperation != null)
        {
            var openApiResponse = originalOperation.Responses
                .FirstOrDefault(r => r.Key == successResponse.Key);
            
            if (openApiResponse.Key != null && openApiResponse.Value.Content != null)
            {
                var content = openApiResponse.Value.Content.FirstOrDefault();
                if (content.Key != null && content.Value?.Schema != null)
                {
                    var responseTypeInfo = _csharpTypeMapper.MapTypeInfo(content.Value.Schema, schemaResolver);
                    return TypeInfo.Task(responseTypeInfo);
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

