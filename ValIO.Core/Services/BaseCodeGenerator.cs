using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Services;

/// <summary>
/// Базовый класс для всех генераторов кода
/// </summary>
public abstract class BaseCodeGenerator : ICodeGenerator
{
    protected readonly ITypeMapper TypeMapper;
    protected readonly INamingConverter NamingConverter;
    protected readonly IFileWriter FileWriter;
    protected readonly ILogger Logger;
    protected readonly GenerationLanguage Language;
    
    // Кэш для поиска операций (path + method -> operation)
    protected Dictionary<string, OpenApiOperation>? OperationCache;
    
    // Кэш для маппинга схем в TypeInfo (schema reference -> TypeInfo)
    protected Dictionary<string, TypeInfo>? SchemaTypeInfoCache;

    protected BaseCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger logger,
        GenerationLanguage language)
    {
        TypeMapper = typeMapper;
        NamingConverter = namingConverter;
        FileWriter = fileWriter;
        Logger = logger;
        Language = language;
    }

    /// <summary>
    /// Поддерживает ли генератор указанный язык
    /// </summary>
    public abstract bool SupportsLanguage(GenerationLanguage language);

    /// <summary>
    /// Поддерживает ли генератор указанные опции
    /// </summary>
    public abstract bool SupportsOptions(GenerationOptions? options);

    /// <summary>
    /// Основной метод генерации кода
    /// </summary>
    public abstract Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config);

    /// <summary>
    /// Создает анализатор зависимостей для текущего языка
    /// </summary>
    protected abstract IDependencyAnalyzer CreateDependencyAnalyzer();

    /// <summary>
    /// Создает построитель TypeInfo для текущего языка
    /// </summary>
    protected abstract ITypeInfoBuilder CreateTypeInfoBuilder(ISchemaResolver schemaResolver);

    /// <summary>
    /// Создает извлекатель enum для текущего языка
    /// </summary>
    protected abstract IEnumExtractor CreateEnumExtractor(ISchemaResolver schemaResolver);

    /// <summary>
    /// Создает стратегию именования для операций текущего языка
    /// </summary>
    protected abstract IOperationNamingStrategy CreateOperationNamingStrategy();

    /// <summary>
    /// Валидирует входные данные для генерации
    /// </summary>
    protected virtual List<string> ValidateInputs(OpenApiDocument? document, GenerationConfiguration? config)
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

        if (config.Language != Language)
        {
            errors.Add($"Language must be {Language}, but got {config.Language}");
        }

        return errors;
    }

    /// <summary>
    /// Создает кэш операций для быстрого поиска
    /// </summary>
    protected virtual void BuildOperationCache(OpenApiDocument document)
    {
        OperationCache = new Dictionary<string, OpenApiOperation>();
        
        if (document.Paths == null)
            return;

        foreach (var pathItem in document.Paths)
        {
            var path = pathItem.Key;
            var operations = pathItem.Value.Operations;
            
            if (operations == null)
                continue;

            foreach (var operation in operations)
            {
                var method = operation.Key.Method;
                var cacheKey = $"{path}:{method}";
                OperationCache[cacheKey] = operation.Value;
            }
        }
    }



    /// <summary>
    /// Инициализирует кэши перед генерацией
    /// </summary>
    protected virtual void InitializeCaches()
    {
        OperationCache = new Dictionary<string, OpenApiOperation>(16 * 1024);
        SchemaTypeInfoCache = new Dictionary<string, TypeInfo>(16 * 1024);
    }

    /// <summary>
    /// Записывает сгенерированные файлы
    /// </summary>
    protected virtual async Task WriteGeneratedFilesAsync(
        Dictionary<string, string> generatedFiles,
        GenerationResult result)
    {
        if (generatedFiles.Count > 0)
        {
            try
            {
                await FileWriter.WriteFilesAsync(generatedFiles);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to write generated files: {ex.Message}";
                var errors = result.Errors.ToList();
                errors.Add(errorMsg);
                result.Errors = errors;
                Logger.LogError(ex, "Failed to write generated files");
            }
        }
        else
        {
            var warnings = result.Warnings.ToList();
            warnings.Add("No files were generated");
            result.Warnings = warnings;
        }
    }

    /// <summary>
    /// Группирует операции по стратегии из конфигурации
    /// </summary>
    protected virtual Dictionary<string, List<OperationTemplateData>> GroupOperations(
        IReadOnlyList<OperationTemplateData> operations, 
        GenerationConfiguration config)
    {
        var groups = new Dictionary<string, List<OperationTemplateData>>(1024);

        switch (config.FileOrganization.GroupingStrategy)
        {
            case FileGroupingStrategy.ByTag:
                foreach (var operation in operations)
                {
                    var tag = operation.Tags?.FirstOrDefault() ?? "Default";
                    var sanitizedTag = NamingConverter.SanitizeIdentifier(tag);
                    if (!groups.ContainsKey(sanitizedTag))
                    {
                        groups[sanitizedTag] = new List<OperationTemplateData>(128);
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
                        groups[routeGroup] = new List<OperationTemplateData>(128);
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
    protected virtual string ExtractRouteGroup(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Default";

        // Убираем параметры пути {id}, {userId} и т.д.
        var pathWithoutParams = System.Text.RegularExpressions.Regex.Replace(path, @"/\{[^}]+\}", "");
        
        // Разбиваем путь на части
        var parts = pathWithoutParams.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return "Default";

        // Берем последнюю часть пути (например, "users" из "/api/users")
        var lastPart = parts[^1];
        
        // Преобразуем в PascalCase
        return NamingConverter.ToPascalCase(lastPart);
    }

    /// <summary>
    /// Категория файла для определения пути
    /// </summary>
    protected enum FileCategory
    {
        Models,
        Clients
    }

    /// <summary>
    /// Разрешает путь к файлу на основе категории и конфигурации
    /// </summary>
    protected virtual string ResolveFilePath(string fileName, GenerationConfiguration config, FileCategory fileCategory = FileCategory.Models)
    {
        var shouldSeparate = fileCategory switch
        {
            FileCategory.Models => config.FileOrganization.SeparateModels,
            FileCategory.Clients => config.FileOrganization.SeparateClients,
            _ => false
        };

        if (shouldSeparate)
        {
            var categoryPath = fileCategory switch
            {
                FileCategory.Models => config.FileOrganization.ModelsPath ?? (Language == GenerationLanguage.TypeScript ? "models" : "Models"),
                FileCategory.Clients => config.FileOrganization.ClientsPath ?? (Language == GenerationLanguage.TypeScript ? "api" : "Api"),
                _ => string.Empty
            };
            
            return Path.Combine(config.OutputPath, categoryPath, fileName);
        }
        
        return Path.Combine(config.OutputPath, fileName);
    }

    /// <summary>
    /// Обрабатывает ошибку генерации и добавляет её в список ошибок
    /// </summary>
    protected virtual void HandleGenerationError(Exception ex, string operationName, List<string> generationErrors, int? itemCount = null)
    {
        var errorMsg = $"Failed to generate {operationName}: {ex.Message}";
        if (ex.InnerException != null)
        {
            errorMsg += $" (Inner: {ex.InnerException.Message})";
        }
        generationErrors.Add(errorMsg);
        
        if (itemCount.HasValue)
        {
            Logger.LogError(ex, "Failed to generate {OperationName}. Count: {Count}", operationName, itemCount.Value);
        }
        else
        {
            Logger.LogError(ex, "Failed to generate {OperationName}", operationName);
        }
    }

    /// <summary>
    /// Структура для извлеченных данных
    /// </summary>
    protected class ExtractedData
    {
        public IReadOnlyList<ModelTemplateData> Models { get; set; } = Array.Empty<ModelTemplateData>();
        public IReadOnlyList<OperationTemplateData> Operations { get; set; } = Array.Empty<OperationTemplateData>();
        public IReadOnlyList<EnumTemplateData> Enums { get; set; } = Array.Empty<EnumTemplateData>();
        public ISchemaResolver SchemaResolver { get; set; } = null!;
    }

    /// <summary>
    /// Извлекает данные из OpenAPI документа (модели, операции, enum'ы)
    /// </summary>
    protected virtual ExtractedData ExtractData(OpenApiDocument document)
    {
        // Создаем SchemaResolver для документа
        var schemaResolver = new SchemaResolver(document);

        // Создаем extractors с SchemaResolver
        var modelExtractor = new ModelExtractor(schemaResolver, NamingConverter);
        var operationExtractor = new OperationExtractor(schemaResolver);
        
        // Создаем язык-специфичные компоненты
        var dependencyAnalyzer = CreateDependencyAnalyzer();
        var typeInfoBuilder = CreateTypeInfoBuilder(schemaResolver);
        var enumExtractor = CreateEnumExtractor(schemaResolver);
        var operationNamingStrategy = CreateOperationNamingStrategy();
        
        // Извлекаем данные
        var allModels = modelExtractor.ExtractModelTemplateData(document, TypeMapper, typeInfoBuilder, dependencyAnalyzer);
        var operations = operationExtractor.ExtractOperationTemplateData(document, TypeMapper, NamingConverter, typeInfoBuilder, dependencyAnalyzer, operationNamingStrategy);
        var enums = enumExtractor.ExtractEnumTemplateData(document);

        // Фильтруем enum схемы из списка моделей
        // Создаем HashSet имен enum'ов для быстрого поиска
        var enumNames = new HashSet<string>(enums.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        
        // Также проверяем схемы напрямую, чтобы не пропустить enum'ы, которые не были извлечены
        var enumSchemaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.Components?.Schemas != null)
        {
            foreach (var schemaEntry in document.Components.Schemas)
            {
                var schemaName = schemaEntry.Key;
                var schema = schemaEntry.Value;
                var resolvedSchema = schemaResolver.ResolveSchema(schema);
                
                // Проверяем, является ли схема enum'ом
                if (BaseEnumExtractor.IsEnumSchema(resolvedSchema))
                {
                    var sanitizedName = NamingConverter.SanitizeIdentifier(schemaName);
                    enumSchemaNames.Add(sanitizedName);
                }
            }
        }
        
        // Объединяем имена enum'ов из обоих источников
        enumNames.UnionWith(enumSchemaNames);
        
        // Фильтруем модели, исключая те, которые являются enum'ами
        var models = allModels.Where(m => !enumNames.Contains(m.Name)).ToList();

        Logger.LogInformation("Extracted {ModelCount} models (filtered from {TotalModelCount}), {OperationCount} operations, {EnumCount} enums", 
            models.Count, allModels.Count, operations.Count, enums.Count);

        return new ExtractedData
        {
            Models = models,
            Operations = operations,
            Enums = enums,
            SchemaResolver = schemaResolver
        };
    }

    /// <summary>
    /// Валидирует извлеченные данные и добавляет предупреждения/ошибки в результат
    /// </summary>
    protected virtual void ValidateExtractedData(ExtractedData data, GenerationConfiguration config, GenerationResult result)
    {
        // Валидируем модели
        ValidateModelTemplateData(data.Models, result);
        
        if (config.GenerateModels && data.Models.Count == 0)
        {
            var warnings = result.Warnings.ToList();
            warnings.Add("No models found in OpenAPI schema");
            result.Warnings = warnings;
        }

        // Валидируем операции
        ValidateOperationTemplateData(data.Operations, result);
        
        if (config.GenerateClients && data.Operations.Count == 0)
        {
            var warnings = result.Warnings.ToList();
            warnings.Add("No operations found in OpenAPI schema");
            result.Warnings = warnings;
        }

        // Валидируем enum'ы
        if (config.GenerateEnums && data.Enums.Count == 0)
        {
            var warnings = result.Warnings.ToList();
            warnings.Add("No enums found in OpenAPI schema");
            result.Warnings = warnings;
        }
    }

    /// <summary>
    /// Валидирует модели TemplateData на пустые имена и дубликаты
    /// </summary>
    protected virtual void ValidateModelTemplateData(IReadOnlyList<ModelTemplateData> models, GenerationResult result)
    {
        var modelNames = new HashSet<string>(16 * 1024);
        var warnings = result.Warnings.ToList();
        var errors = result.Errors.ToList();

        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                errors.Add("Model has empty name");
                continue;
            }

            var modelName = model.Name;
            if (modelNames.Contains(modelName))
            {
                var warning = $"Duplicate model name '{modelName}' found";
                warnings.Add(warning);
                Logger.LogWarning(warning);
            }
            else
            {
                modelNames.Add(modelName);
            }
        }

        result.Warnings = warnings;
        if (errors.Count > 0)
        {
            var existingErrors = result.Errors.ToList();
            existingErrors.AddRange(errors);
            result.Errors = existingErrors;
        }
    }

    /// <summary>
    /// Валидирует операции TemplateData на дублирующиеся operationId и дублирующиеся параметры
    /// </summary>
    protected virtual void ValidateOperationTemplateData(IReadOnlyList<OperationTemplateData> operations, GenerationResult result)
    {
        var operationIds = new Dictionary<string, List<string>>(16 * 1024);
        var warnings = result.Warnings.ToList();
        var errors = new List<string>(8192);

        foreach (var operation in operations)
        {
            var operationId = operation.MethodName;
            if (string.IsNullOrWhiteSpace(operationId))
            {
                errors.Add("Operation has empty method name");
                continue;
            }

            var location = $"{operation.HttpMethod} {operation.Path}";
            if (!operationIds.ContainsKey(operationId))
            {
                operationIds[operationId] = new List<string>();
            }
            operationIds[operationId].Add(location);
            
            // Проверяем дубликаты параметров
            var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateParams = new List<string>();
            
            // Проверяем path parameters
            foreach (var param in operation.PathParameters)
            {
                if (!parameterNames.Add(param.VariableName))
                {
                    duplicateParams.Add($"path parameter '{param.VariableName}'");
                }
            }
            
            // Проверяем query parameters
            foreach (var param in operation.QueryParameters)
            {
                if (!parameterNames.Add(param.VariableName))
                {
                    duplicateParams.Add($"query parameter '{param.VariableName}'");
                }
            }
            
            // Проверяем header parameters
            foreach (var param in operation.HeaderParameters)
            {
                if (!parameterNames.Add(param.VariableName))
                {
                    duplicateParams.Add($"header parameter '{param.VariableName}'");
                }
            }
            
            if (duplicateParams.Count > 0)
            {
                var warning = $"Operation '{operationId}' ({location}) has duplicate parameter names: {string.Join(", ", duplicateParams)}";
                warnings.Add(warning);
                Logger.LogWarning(warning);
            }
        }

        // Проверяем дубликаты operationId
        foreach (var kvp in operationIds)
        {
            if (kvp.Value.Count > 1)
            {
                var warning = $"Duplicate operationId '{kvp.Key}' found at: {string.Join(", ", kvp.Value)}";
                warnings.Add(warning);
                Logger.LogWarning(warning);
            }
        }

        result.Warnings = warnings;
        if (errors.Count > 0)
        {
            var existingErrors = result.Errors.ToList();
            existingErrors.AddRange(errors);
            result.Errors = existingErrors;
        }
    }
}
