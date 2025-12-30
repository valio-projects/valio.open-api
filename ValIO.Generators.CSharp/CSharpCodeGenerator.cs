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

            // Извлекаем операции
            var operations = operationExtractor.ExtractOperations(document);
            _logger.LogInformation("Extracted {Count} operations", operations.Count);

            // Маппим типы для моделей
            foreach (var model in models)
            {
                MapModelTypes(model, schemaResolver);
            }

            // Маппим типы для операций
            foreach (var operation in operations)
            {
                MapOperationTypes(operation, document, schemaResolver);
            }

            // Извлекаем enum'ы
            var enums = enumExtractor.ExtractEnums(document);
            _logger.LogInformation("Extracted {Count} enums", enums.Count);

            // Генерируем файлы
            var generatedFiles = new Dictionary<string, string>();

            if (config.GenerateEnums)
            {
                var enumFiles = GenerateEnums(enums, config);
                foreach (var file in enumFiles)
                {
                    generatedFiles[file.Key] = file.Value;
                }
            }

            if (config.GenerateModels)
            {
                var modelFiles = GenerateModels(models, config);
                foreach (var file in modelFiles)
                {
                    generatedFiles[file.Key] = file.Value;
                }
            }

            if (config.GenerateClients)
            {
                var clientFiles = GenerateClients(operations, config);
                foreach (var file in clientFiles)
                {
                    generatedFiles[file.Key] = file.Value;
                }
            }

            // Записываем файлы
            await _fileWriter.WriteFilesAsync(generatedFiles);

            result.Success = true;
            result.GeneratedFiles = generatedFiles;
            _logger.LogInformation("C# code generation completed successfully. Generated {Count} files", generatedFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during C# code generation");
            result.Errors = new List<string> { ex.Message };
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
            var filePath = config.FileOrganization.SeparateModels && !string.IsNullOrEmpty(config.FileOrganization.ModelsPath)
                ? Path.Combine(config.OutputPath, config.FileOrganization.ModelsPath, fileName)
                : Path.Combine(config.OutputPath, fileName);

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
            Description = v.Description,
            ValueString = FormatEnumValue(v.Value, apiEnum.UnderlyingType)
        }).ToList();

        var templateData = new EnumTemplateData
        {
            Namespace = namespaceName,
            EnumName = enumName,
            Description = apiEnum.Description,
            UnderlyingType = apiEnum.UnderlyingType,
            Values = enumValues
        };

        return _templateRegistry.Render("csharp-enum.sbncs", templateData);
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
            var filePath = config.FileOrganization.SeparateModels && !string.IsNullOrEmpty(config.FileOrganization.ModelsPath)
                ? Path.Combine(config.OutputPath, config.FileOrganization.ModelsPath, fileName)
                : Path.Combine(config.OutputPath, fileName);

            files[filePath] = modelCode;
        }

        return files;
    }

    private string GenerateModelCode(ApiModel model, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = _namingConverter.ToPascalCase(model.Name);

        var properties = model.Properties.Select(p => new PropertyTemplateData
        {
            Name = _namingConverter.ToPascalCase(p.Name),
            Type = p.Type,
            Description = p.Description
        }).ToList();

        var templateData = new ModelTemplateData
        {
            Namespace = namespaceName,
            ClassName = className,
            Description = model.Description,
            Properties = properties
        };

        return _templateRegistry.Render("csharp-model.sbncs", templateData);
    }

    private Dictionary<string, string> GenerateClients(IReadOnlyList<ApiOperation> operations, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        // Группируем операции по тегам или создаем один клиент
        var groupedOperations = GroupOperations(operations, config);

        foreach (var group in groupedOperations)
        {
            var clientCode = GenerateClientCode(group.Value, group.Key, config);
            var fileName = $"{_namingConverter.ToPascalCase(group.Key)}Client.cs";
            var filePath = config.FileOrganization.SeparateClients && !string.IsNullOrEmpty(config.FileOrganization.ClientsPath)
                ? Path.Combine(config.OutputPath, config.FileOrganization.ClientsPath, fileName)
                : Path.Combine(config.OutputPath, fileName);

            files[filePath] = clientCode;
        }

        return files;
    }

    private Dictionary<string, List<ApiOperation>> GroupOperations(IReadOnlyList<ApiOperation> operations, GenerationConfiguration config)
    {
        var groups = new Dictionary<string, List<ApiOperation>>();

        if (config.FileOrganization.GroupingStrategy == FileGroupingStrategy.ByTag)
        {
            foreach (var operation in operations)
            {
                var tag = operation.Tags.FirstOrDefault() ?? "Default";
                if (!groups.ContainsKey(tag))
                {
                    groups[tag] = new List<ApiOperation>();
                }
                groups[tag].Add(operation);
            }
        }
        else
        {
            // SingleFile - все операции в одном клиенте
            groups["Api"] = operations.ToList();
        }

        return groups;
    }

    private string GenerateClientCode(List<ApiOperation> operations, string clientName, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = $"{_namingConverter.ToPascalCase(clientName)}Client";

        var operationTemplates = operations.Select(op => CreateOperationTemplateData(op, config)).ToList();

        var templateData = new ClientTemplateData
        {
            Namespace = namespaceName,
            ClassName = className,
            ClientName = clientName,
            Operations = operationTemplates
        };

        return _templateRegistry.Render("csharp-client.sbncs", templateData);
    }

    private OperationTemplateData CreateOperationTemplateData(ApiOperation operation, GenerationConfiguration config)
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
            var paramType = parameter.Type;
            
            // Если тип не определен или базовый, пытаемся определить его
            if (string.IsNullOrEmpty(paramType) || paramType == "object" || paramType == "string")
            {
                // Для параметров обычно используется базовый тип, но можно улучшить
                paramType = paramType == "object" ? "object" : "string";
            }
            
            var paramModifier = parameter.IsRequired ? "" : "?";
            var paramString = $"{paramType}{paramModifier} {paramName}";
            allParameters.Add(paramString);

            var paramTemplate = new ParameterTemplateData
            {
                Name = parameter.Name,
                VariableName = paramName,
                Type = paramType,
                IsRequired = parameter.IsRequired,
                Description = parameter.Description
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
        
        if (operation.RequestBody != null)
        {
            requestBodyType = operation.RequestBody.Type;
            if (string.IsNullOrEmpty(requestBodyType) || requestBodyType == "object")
            {
                requestBodyType = "object";
            }
            
            requestBodyContentType = operation.RequestBody.ContentType ?? "application/json";
            allParameters.Add($"{requestBodyType} {requestBodyParameter}");
        }

        // Определяем тип ответа на основе успешных ответов (2xx)
        var returnType = DetermineReturnType(operation, config);

        return new OperationTemplateData
        {
            MethodName = methodName,
            HttpMethod = httpMethod,
            Path = path,
            Summary = operation.Summary,
            Parameters = string.Join(", ", allParameters),
            ReturnType = returnType,
            HasRequestBody = operation.RequestBody != null,
            RequestBodyParameter = requestBodyParameter,
            RequestBodyContentType = requestBodyContentType,
            HasResponseType = !string.IsNullOrEmpty(returnType) && returnType != "Task<HttpResponseMessage>" && returnType != "Task<void>",
            HasPathParameters = pathParameters.Count > 0,
            HasQueryParameters = queryParameters.Count > 0,
            HasHeaderParameters = headerParameters.Count > 0,
            PathParameters = pathParameters,
            QueryParameters = queryParameters,
            HeaderParameters = headerParameters
        };
    }

    private string DetermineReturnType(ApiOperation operation, GenerationConfiguration config)
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
            return "Task<HttpResponseMessage>";
        }

        var responseType = successResponse.Value.Type;

        if (string.IsNullOrEmpty(responseType))
        {
            // Если тип не определен, возвращаем HttpResponseMessage
            return "Task<HttpResponseMessage>";
        }

        // Если тип void или пустой, возвращаем Task
        if (responseType == "void" || responseType == "null")
        {
            return "Task";
        }

        // Возвращаем Task<T>
        return $"Task<{responseType}>";
    }

    private async Task EnsureTemplatesPreloadedAsync()
    {
        if (_templatesPreloaded)
            return;

        try
        {
            // Загружаем шаблоны
            if (!_templateRegistry.HasTemplate("csharp-model.sbncs") || 
                !_templateRegistry.HasTemplate("csharp-client.sbncs") ||
                !_templateRegistry.HasTemplate("csharp-enum.sbncs"))
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

