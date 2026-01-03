using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using System.Linq;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Генератор кода для C#
/// </summary>
public class CSharpCodeGenerator : BaseCodeGenerator
{
    private readonly CSharpTypeMapper _csharpTypeMapper;
    private readonly CSharpCodeRenderer _codeRenderer;

    public CSharpCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<CSharpCodeGenerator> logger)
        : base(typeMapper, namingConverter, fileWriter, logger, GenerationLanguage.CSharp)
    {
        _csharpTypeMapper = typeMapper as CSharpTypeMapper ?? throw new InvalidOperationException("TypeMapper must be CSharpTypeMapper");
        _codeRenderer = new CSharpCodeRenderer();
    }

    public override bool SupportsLanguage(GenerationLanguage language)
    {
        return language == GenerationLanguage.CSharp;
    }

    public override bool SupportsOptions(GenerationOptions? options)
    {
        // C# генератор поддерживает любые опции (или их отсутствие)
        return true;
    }

    public override async Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config)
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

            // Шаблоны больше не используются - используется CSharpCodeRenderer

            Logger.LogInformation("Starting C# code generation");

            // Инициализируем кэши
            InitializeCaches();
            
            // Предзаполняем кэш операций для быстрого поиска
            BuildOperationCache(document);

            // Создаем SchemaResolver для документа
            var schemaResolver = new SchemaResolver(document);

            // Создаем extractors с SchemaResolver
            var modelExtractor = new ModelExtractor(schemaResolver, NamingConverter);
            var operationExtractor = new OperationExtractor(schemaResolver);
            var enumExtractor = new EnumExtractor(schemaResolver, NamingConverter);

            // Извлекаем модели в виде TemplateData
            var modelTemplateDataList = modelExtractor.ExtractModelTemplateData(document, GenerationLanguage.CSharp, _csharpTypeMapper);
            Logger.LogInformation("Extracted {Count} models", modelTemplateDataList.Count);
            
            if (config.GenerateModels && modelTemplateDataList.Count == 0)
            {
                result.Warnings = new List<string> { "No models found in OpenAPI schema" };
            }
            
            // Валидируем модели на пустые имена
            ValidateModelTemplateData(modelTemplateDataList, result);

            // Извлекаем операции в виде TemplateData
            var operationTemplateDataList = operationExtractor.ExtractOperationTemplateData(document, GenerationLanguage.CSharp, _csharpTypeMapper, NamingConverter);
            Logger.LogInformation("Extracted {Count} operations", operationTemplateDataList.Count);
            
            if (config.GenerateClients && operationTemplateDataList.Count == 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.Add("No operations found in OpenAPI schema");
                result.Warnings = warnings;
            }
            
            // Валидируем операции на дублирующиеся operationId
            ValidateOperationTemplateData(operationTemplateDataList, result);

            // Маппинг типов теперь выполняется в extractors при создании TemplateData

            // Извлекаем enum в виде TemplateData
            var enumTemplateDataList = enumExtractor.ExtractEnumTemplateData(document, GenerationLanguage.CSharp);
            Logger.LogInformation("Extracted {Count} enums", enumTemplateDataList.Count);
            
            if (config.GenerateEnums && enumTemplateDataList.Count == 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.Add("No enums found in OpenAPI schema");
                result.Warnings = warnings;
            }

            // Ошибки маппинга обрабатываются в extractors

            // Генерируем файлы
            var generatedFiles = new Dictionary<string, string>();
            var generationErrors = new List<string>();
            var generationWarnings = new List<string>();

            if (config.GenerateEnums)
            {
                try
                {
                    var enumFiles = GenerateEnums(enumTemplateDataList, config);
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
                    Logger.LogError(ex, "Failed to generate enums. Count: {Count}", enumTemplateDataList.Count);
                }
            }

            if (config.GenerateModels)
            {
                try
                {
                    var modelFiles = GenerateModels(modelTemplateDataList, config);
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
                    Logger.LogError(ex, "Failed to generate models. Count: {Count}", modelTemplateDataList.Count);
                }
            }

            if (config.GenerateClients)
            {
                try
                {
                    var clientFiles = GenerateClients(operationTemplateDataList, config);
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
                    Logger.LogError(ex, "Failed to generate clients. Operations count: {Count}", operationTemplateDataList.Count);
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
                Logger.LogInformation("C# code generation completed successfully. Generated {Count} files", generatedFiles.Count);
            }
            else
            {
                Logger.LogWarning("C# code generation completed with errors. Generated {Count} files, {ErrorCount} errors", 
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
                Logger.LogError(ex, "Error during C# code generation. Stack trace: {StackTrace}", ex.StackTrace);
            }
            else
            {
                Logger.LogError(ex, "Error during C# code generation");
            }
            result.Errors = new List<string> { errorMessage };
            result.Success = false;
        }

        return result;
    }

    // Методы MapModelTypes и MapOperationTypes удалены - маппинг теперь выполняется в extractors

    /// <summary>
    /// Переопределяет BuildOperationCache для более сложной логики с operationId
    /// </summary>
    protected override void BuildOperationCache(OpenApiDocument document)
    {
        OperationCache = new Dictionary<string, OpenApiOperation>();
        
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

                // Создаем ключ: operationId + method + path
                var cacheKey = $"{operationId}|{operationEntry.Key.Method}|{pathEntry.Key}";
                OperationCache[cacheKey] = openApiOperation;
            }
        }
        
        Logger.LogDebug("Built operation cache with {Count} entries", OperationCache.Count);
    }


    private Dictionary<string, string> GenerateEnums(IReadOnlyList<Core.Models.TemplateData.EnumTemplateData> enumTemplateDataList, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();
        var enumRenderer = new Renderers.CSharpEnumRenderer();

        foreach (var enumTemplateData in enumTemplateDataList)
        {
            // Валидируем имя enum
            if (string.IsNullOrWhiteSpace(enumTemplateData.Name))
            {
                Logger.LogWarning("Skipping enum with empty name");
                continue;
            }
            
            // Устанавливаем namespace из конфигурации, если не установлен
            if (string.IsNullOrEmpty(enumTemplateData.Namespace))
            {
                enumTemplateData.Namespace = config.Namespace ?? "Generated";
            }
            
            var enumCode = enumRenderer.Render(enumTemplateData);
            var fileName = $"{enumTemplateData.Name}.cs";
            
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

    private Dictionary<string, string> GenerateModels(IReadOnlyList<Core.Models.TemplateData.ModelTemplateData> modelTemplateDataList, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();
        var csharpOptions = GetCSharpOptions(config);

        foreach (var modelTemplateData in modelTemplateDataList)
        {
            // Валидируем имя модели
            if (string.IsNullOrWhiteSpace(modelTemplateData.Name))
            {
                Logger.LogWarning("Skipping model with empty name");
                continue;
            }
            
            // Устанавливаем namespace из конфигурации, если не установлен
            if (string.IsNullOrEmpty(modelTemplateData.Namespace))
            {
                modelTemplateData.Namespace = config.Namespace ?? "Generated";
            }
            
            // Устанавливаем формат модели из опций конфигурации
            if (!modelTemplateData.Options.ContainsKey("ModelType"))
            {
                modelTemplateData.Format = csharpOptions.ModelType switch
                {
                    ModelType.Record => ModelFormat.Record,
                    ModelType.Struct => ModelFormat.Struct,
                    _ => ModelFormat.Class
                };
                modelTemplateData.Options["ModelType"] = csharpOptions.ModelType;
                modelTemplateData.Options["IsSealed"] = csharpOptions.UseSealed;
                modelTemplateData.Options["GenerateEqualityMembers"] = csharpOptions.GenerateEqualityMembers;
                modelTemplateData.Options["GenerateValidationConstants"] = csharpOptions.GenerateValidationConstants;
                modelTemplateData.Options["GenerateValidationAttributes"] = csharpOptions.GenerateValidationAttributes;
            }
            
            // Выбираем renderer на основе формата
            IModelFormatRenderer? modelRenderer = modelTemplateData.Format switch
            {
                ModelFormat.Record => new Renderers.RecordModelRenderer(),
                ModelFormat.Class => new Renderers.ClassModelRenderer(),
                _ => new Renderers.ClassModelRenderer()
            };
            
            var modelCode = modelRenderer.Render(modelTemplateData);
            var fileName = $"{modelTemplateData.Name}.cs";
            
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

    // Метод GenerateModelCode удален - модели теперь извлекаются в виде TemplateData напрямую из extractors

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

    private Dictionary<string, string> GenerateClients(IReadOnlyList<Core.Models.TemplateData.OperationTemplateData> operationTemplateDataList, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        // Собираем все уникальные исключения из всех операций
        var exceptionClasses = new HashSet<string>();
        var exceptionDataMap = new Dictionary<string, ExceptionTemplateData>();

        // Группируем операции по тегам или создаем один клиент
        var groupedOperations = GroupOperations(operationTemplateDataList, config);

        // Получаем C# опции для определения стиля query параметров
        var csharpOptions = GetCSharpOptions(config);
        var useQueryParametersModel = csharpOptions.QueryParametersStyle == QueryParametersStyle.ModelClass;

        // Собираем информацию о всех исключениях и моделях query параметров
        var queryParametersModels = new Dictionary<string, List<Core.Models.TemplateData.ParameterTemplateData>>();
        foreach (var operationTemplate in operationTemplateDataList)
        {
            // Собираем модели query параметров для генерации
            if (useQueryParametersModel && operationTemplate.QueryParameters.Count > 0 && !string.IsNullOrEmpty(operationTemplate.QueryParametersModelTypeName))
            {
                if (!string.IsNullOrEmpty(operationTemplate.QueryParametersModelTypeName) && !queryParametersModels.ContainsKey(operationTemplate.QueryParametersModelTypeName))
                {
                    queryParametersModels[operationTemplate.QueryParametersModelTypeName] = operationTemplate.QueryParameters.ToList();
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
            var clientCode = GenerateClientCode(group.Value, group.Key, config);
            var fileName = $"{NamingConverter.ToPascalCase(group.Key)}Client.cs";
            
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
    private string GenerateQueryParametersModelCode(string className, List<Core.Models.TemplateData.ParameterTemplateData> parameters, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var csharpOptions = GetCSharpOptions(config);

        var properties = new List<PropertyTemplateData>();
        foreach (var param in parameters)
        {
            var property = new PropertyTemplateData
            {
                Name = NamingConverter.ToPascalCase(param.Name),
                TypeInfo = param.TypeInfo,
                IsRequired = param.IsRequired,
                Description = param.Description,
                IsNullable = !param.IsRequired && !param.TypeInfo.IsNonNullableValueType()
            };
            
            // Добавляем C# специфичные опции
            property.Options["AccessorType"] = csharpOptions.PropertyAccessor;
            
            properties.Add(property);
        }

        var templateData = new Core.Models.TemplateData.ModelTemplateData
        {
            Name = className,
            Namespace = namespaceName,
            Properties = properties,
            Language = GenerationLanguage.CSharp,
            Format = csharpOptions.ModelType switch
            {
                ModelType.Record => ModelFormat.Record,
                ModelType.Struct => ModelFormat.Struct,
                _ => ModelFormat.Class
            },
            Options = new Dictionary<string, object>
            {
                ["ModelType"] = csharpOptions.ModelType,
                ["IsSealed"] = csharpOptions.UseSealed,
                ["GenerateEqualityMembers"] = csharpOptions.GenerateEqualityMembers,
                ["GenerateValidationConstants"] = csharpOptions.GenerateValidationConstants,
                ["GenerateValidationAttributes"] = csharpOptions.GenerateValidationAttributes
            }
        };

        return _codeRenderer.RenderModel(templateData);
    }

    private string GenerateExceptionCode(ExceptionTemplateData exceptionData)
    {
        var sb = new System.Text.StringBuilder();
        
        // Генерируем namespace
        if (!string.IsNullOrEmpty(exceptionData.Namespace))
        {
            sb.AppendLine($"namespace {exceptionData.Namespace};");
            sb.AppendLine();
        }
        
        // Генерируем XML комментарий
        if (!string.IsNullOrEmpty(exceptionData.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {EscapeXmlComment(exceptionData.Description)}");
            sb.AppendLine("/// </summary>");
        }
        
        // Генерируем класс исключения
        var responseType = exceptionData.ResponseTypeInfo != null 
            ? exceptionData.ResponseTypeInfo.FullTypeName 
            : "object";
        
        sb.AppendLine($"public class {exceptionData.ClassName} : ValioHttpRequestException");
        sb.AppendLine("{");
        sb.AppendLine($"    public {responseType}? ErrorResponse {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"    public {exceptionData.ClassName}(HttpResponseMessage response, {responseType}? errorResponse, Exception? innerException, string? operationName, string? httpMethod, string? requestPath)");
        sb.AppendLine("        : base($\"HTTP request failed with status code {response.StatusCode}\", response, innerException, operationName, httpMethod, requestPath)");
        sb.AppendLine("    {");
        sb.AppendLine("        ErrorResponse = errorResponse;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public {exceptionData.ClassName}(HttpResponseMessage response, {responseType}? errorResponse, string? operationName, string? httpMethod, string? requestPath)");
        sb.AppendLine("        : base($\"HTTP request failed with status code {response.StatusCode}\", response, operationName, httpMethod, requestPath)");
        sb.AppendLine("    {");
        sb.AppendLine("        ErrorResponse = errorResponse;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public {exceptionData.ClassName}(HttpResponseMessage response, string? operationName, string? httpMethod, string? requestPath)");
        sb.AppendLine("        : base($\"HTTP request failed with status code {response.StatusCode}\", response, operationName, httpMethod, requestPath)");
        sb.AppendLine("    {");
        sb.AppendLine("        ErrorResponse = null;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private static string EscapeXmlComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
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

    private Dictionary<string, List<Core.Models.TemplateData.OperationTemplateData>> GroupOperations(IReadOnlyList<Core.Models.TemplateData.OperationTemplateData> operations, GenerationConfiguration config)
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
                    // Извлекаем базовый путь из route (например, /api/users -> Users)
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
        var sanitized = NamingConverter.SanitizeIdentifier(lastSegment);
        return NamingConverter.ToPascalCase(sanitized);
    }

    private string GenerateClientCode(List<Core.Models.TemplateData.OperationTemplateData> operations, string clientName, GenerationConfiguration config)
    {
        var namespaceName = config.Namespace ?? "Generated";
        var className = $"{NamingConverter.ToPascalCase(clientName)}Client";

        var templateData = new Core.Models.TemplateData.ClientTemplateData
        {
            Name = className,
            Namespace = namespaceName,
            Operations = operations,
            Language = GenerationLanguage.CSharp,
            // HttpClientLibrary используется только для TypeScript (Fetch, Axios)
            // Для C# всегда используется HttpClient из System.Net.Http
            HttpClientLibrary = HttpClientLibrary.Fetch, // Значение по умолчанию, не используется для C#
            Options = new Dictionary<string, object>
            {
                ["ClientName"] = clientName,
                ["HttpClientType"] = "HttpClient" // Указываем, что для C# используется HttpClient
            }
        };

        return _codeRenderer.RenderClient(templateData);
    }

    // Метод CreateOperationTemplateData удален - операции теперь извлекаются в виде TemplateData напрямую из extractors

    /// <summary>
    /// Создает Task тип для C# (helper метод для C# специфичных типов)
    /// </summary>
    private static TypeInfo CreateTaskType(TypeInfo? resultType = null, bool isNullable = false)
    {
        if (resultType == null)
        {
            return new TypeInfo
            {
                BaseType = "Task",
                IsNullable = isNullable,
                FullTypeName = "Task",
                Language = GenerationLanguage.CSharp,
                Format = TypeFormat.Object
            };
        }

        var fullTypeName = $"Task<{resultType.FullTypeName}>";
        if (isNullable)
        {
            fullTypeName += "?";
        }

        return new TypeInfo
        {
            BaseType = "Task",
            IsNullable = isNullable,
            GenericArguments = new[] { resultType },
            FullTypeName = fullTypeName,
            Language = GenerationLanguage.CSharp,
            Dependencies = resultType.Dependencies,
            Format = TypeFormat.Object
        };
    }

    /// <summary>
    /// Создает HttpResponseMessage тип для C# (helper метод для C# специфичных типов)
    /// </summary>
    private static TypeInfo CreateHttpResponseMessageType(bool isNullable = false)
    {
        return new TypeInfo
        {
            BaseType = "HttpResponseMessage",
            IsNullable = isNullable,
            FullTypeName = isNullable ? "HttpResponseMessage?" : "HttpResponseMessage",
            Language = GenerationLanguage.CSharp,
            Format = TypeFormat.Object
        };
    }

    /// <summary>
    /// Определяет тип возврата операции через TypeInfo
    /// </summary>
    // Методы DetermineReturnTypeInfo, DetermineRequestBodyTypeInfo, FindSchemaInContent, MapSchemaToTypeInfo, AreSchemasSimilar удалены - маппинг теперь выполняется в extractors

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
        return $"{methodPrefix}{NamingConverter.ToPascalCase(lastSegment)}";
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


    // Метод EnsureTemplatesPreloadedAsync удален - шаблоны больше не используются
    
    /// <summary>
    /// Валидирует модели TemplateData на пустые имена
    /// </summary>
    private void ValidateModelTemplateData(IReadOnlyList<Core.Models.TemplateData.ModelTemplateData> models, GenerationResult result)
    {
        var errors = new List<string>();
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                errors.Add("Model has empty name");
            }
        }
        
        if (errors.Count > 0)
        {
            var existingErrors = result.Errors.ToList();
            existingErrors.AddRange(errors);
            result.Errors = existingErrors;
        }
    }
    
    /// <summary>
    /// Валидирует операции TemplateData на дублирующиеся operationId
    /// </summary>
    private void ValidateOperationTemplateData(IReadOnlyList<Core.Models.TemplateData.OperationTemplateData> operations, GenerationResult result)
    {
        var operationIds = new HashSet<string>();
        var duplicates = new List<string>();
        
        foreach (var operation in operations)
        {
            var operationId = operation.MethodName;
            if (string.IsNullOrWhiteSpace(operationId))
            {
                duplicates.Add("Operation has empty method name");
                continue;
            }
            
            if (operationIds.Contains(operationId))
            {
                duplicates.Add($"Duplicate operationId: {operationId}");
            }
            else
            {
                operationIds.Add(operationId);
            }
        }
        
        if (duplicates.Count > 0)
        {
            var existingWarnings = result.Warnings.ToList();
            existingWarnings.AddRange(duplicates);
            result.Warnings = existingWarnings;
        }
    }
}

