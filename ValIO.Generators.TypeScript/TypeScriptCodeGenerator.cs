using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Models;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Базовый генератор кода для TypeScript
/// </summary>
public class TypeScriptCodeGenerator : ICodeGenerator
{
    private readonly IModelExtractor _modelExtractor;
    private readonly IOperationExtractor _operationExtractor;
    private readonly ITypeMapper _typeMapper;
    private readonly INamingConverter _namingConverter;
    private readonly IFileWriter _fileWriter;
    private readonly ILogger<TypeScriptCodeGenerator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ITemplateRegistry? _templateRegistry;
    private bool _templatesPreloaded;

    public TypeScriptCodeGenerator(
        IModelExtractor modelExtractor,
        IOperationExtractor operationExtractor,
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<TypeScriptCodeGenerator> logger, IServiceProvider serviceProvider)
    {
        _modelExtractor = modelExtractor;
        _operationExtractor = operationExtractor;
        _typeMapper = typeMapper;
        _namingConverter = namingConverter;
        _fileWriter = fileWriter;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _templatesPreloaded = false;
    }

    public bool SupportsLanguage(GenerationLanguage language)
    {
        return language == GenerationLanguage.TypeScript;
    }

    public bool SupportsOptions(GenerationOptions? options)
    {
        // Базовый TypeScript генератор поддерживает только базовые опции (без библиотек)
        return options == null || 
               (options.ValidationLibrary == null && 
                options.HttpClientLibrary == null && 
                options.QueryWrapperLibrary == null);
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

            _logger.LogInformation("Starting TypeScript code generation");

            // Создаем SchemaResolver для документа
            var schemaResolver = new SchemaResolver(document);

            // Извлекаем модели
            var models = _modelExtractor.ExtractModels(document);
            _logger.LogInformation("Extracted {Count} models", models.Count);

            // Извлекаем операции
            var operations = _operationExtractor.ExtractOperations(document);
            _logger.LogInformation("Extracted {Count} operations", operations.Count);

            // Маппим типы для моделей
            foreach (var model in models)
            {
                MapModelTypes(model, schemaResolver);
            }

            // Маппим типы для операций
            foreach (var operation in operations)
            {
                MapOperationTypes(operation, schemaResolver);
            }

            // Генерируем файлы
            var generatedFiles = new Dictionary<string, string>();

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
            _logger.LogInformation("TypeScript code generation completed successfully. Generated {Count} files", generatedFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TypeScript code generation");
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

    private void MapOperationTypes(ApiOperation operation, ISchemaResolver schemaResolver)
    {
        // Маппим типы параметров
        // TODO: Реализовать маппинг типов параметров через ISchemaResolver

        // Маппим тип request body
        if (operation.RequestBody != null)
        {
            // TODO: Реализовать маппинг типа request body
        }

        // Маппим типы ответов
        foreach (var response in operation.Responses.Values)
        {
            // TODO: Реализовать маппинг типов ответов
        }
    }

    private Dictionary<string, string> GenerateModels(IReadOnlyList<ApiModel> models, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        foreach (var model in models)
        {
            var modelCode = GenerateModelCode(model, config);
            var fileName = $"{_namingConverter.ToCamelCase(model.Name)}.model.ts";
            var filePath = config.FileOrganization.SeparateModels && !string.IsNullOrEmpty(config.FileOrganization.ModelsPath)
                ? Path.Combine(config.OutputPath, config.FileOrganization.ModelsPath, fileName)
                : Path.Combine(config.OutputPath, fileName);

            files[filePath] = modelCode;
        }

        return files;
    }

    private string GenerateModelCode(ApiModel model, GenerationConfiguration config)
    {
        var interfaceName = _namingConverter.ToPascalCase(model.Name);

        var properties = model.Properties.Select(p => new TypeScriptPropertyTemplateData
        {
            Name = _namingConverter.ToCamelCase(p.Name),
            Type = p.Type,
            Optional = p.IsRequired ? "" : "?",
            Description = p.Description
        }).ToList();

        var templateData = new InterfaceTemplateData
        {
            InterfaceName = interfaceName,
            Description = model.Description,
            Properties = properties
        };

        if (_templateRegistry == null)
            throw new InvalidOperationException("TemplateRegistry is not initialized. Call EnsureTemplatesPreloadedAsync first.");
        
        return _templateRegistry.Render("typescript-interface.sbncs", templateData);
    }

    private Dictionary<string, string> GenerateClients(IReadOnlyList<ApiOperation> operations, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();

        // Группируем операции по тегам или создаем один клиент
        var groupedOperations = GroupOperations(operations, config);

        foreach (var group in groupedOperations)
        {
            var clientCode = GenerateClientCode(group.Value, group.Key, config);
            var fileName = $"{_namingConverter.ToCamelCase(group.Key)}.api.ts";
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
        var operationTemplates = operations.Select(op => CreateTypeScriptOperationTemplateData(op, config)).ToList();

        var templateData = new ApiTemplateData
        {
            Operations = operationTemplates
        };

        if (_templateRegistry == null)
            throw new InvalidOperationException("TemplateRegistry is not initialized. Call EnsureTemplatesPreloadedAsync first.");
        
        return _templateRegistry.Render("typescript-api.sbncs", templateData);
    }

    private TypeScriptOperationTemplateData CreateTypeScriptOperationTemplateData(ApiOperation operation, GenerationConfiguration config)
    {
        var methodName = _namingConverter.ToCamelCase(operation.OperationId);
        var httpMethod = operation.HttpMethod.Method.ToUpperInvariant();
        var path = operation.Path;

        // Параметры метода
        var parameters = new List<string>();
        foreach (var parameter in operation.Parameters)
        {
            var paramName = _namingConverter.ToCamelCase(parameter.Name);
            var paramType = parameter.Type;
            var paramOptional = parameter.IsRequired ? "" : "?";
            parameters.Add($"{paramName}{paramOptional}: {paramType}");
        }

        var requestBodyParameter = "request";
        if (operation.RequestBody != null)
        {
            var bodyType = operation.RequestBody.Type;
            parameters.Add($"{requestBodyParameter}: {bodyType}");
        }

        var returnType = "Promise<Response>"; // TODO: Определить тип ответа

        return new TypeScriptOperationTemplateData
        {
            MethodName = methodName,
            HttpMethod = httpMethod,
            Path = path,
            Summary = operation.Summary,
            Parameters = string.Join(", ", parameters),
            ReturnType = returnType,
            HasRequestBody = operation.RequestBody != null,
            RequestBodyParameter = requestBodyParameter
        };
    }

    private async Task EnsureTemplatesPreloadedAsync()
    {
        if (_templatesPreloaded && _templateRegistry != null)
            return;

        try
        {
            // Определяем путь к директории с шаблонами
            var assemblyLocation = Path.GetDirectoryName(typeof(TypeScriptCodeGenerator).Assembly.Location) ?? string.Empty;
            var templatesPath = Path.Combine(assemblyLocation, "Templates");

            // Если шаблоны не найдены в выходной директории, пробуем другие места
            if (!Directory.Exists(templatesPath))
            {
                var possiblePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Templates"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Templates"),
                    Path.Combine(assemblyLocation, "Templates")
                };

                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        templatesPath = path;
                        break;
                    }
                }
            }

            // Создаем TemplateRegistry из файловой системы
            if (_templateRegistry == null)
            {
                var cache = new TemplateCache();
                var logger = _serviceProvider.GetRequiredService<ILogger<FileSystemTemplateRegistry>>();
                _templateRegistry = new FileSystemTemplateRegistry(templatesPath, cache, logger);
            }

            // Загружаем шаблоны
            if (!_templateRegistry.HasTemplate("typescript-interface.sbncs") || !_templateRegistry.HasTemplate("typescript-api.sbncs"))
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

