using System;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Models;
using System.Text;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Базовый генератор кода для TypeScript
/// </summary>
public class TypeScriptCodeGenerator : BaseCodeGenerator
{
    private readonly TypeScriptTypeMapper _typescriptTypeMapper;
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
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _queryWrapperRenderer = queryWrapperRenderer;
        _zodModelRenderer = zodModelRenderer;
        _zodEnumRenderer = zodEnumRenderer;
    }

    /// <summary>
    /// Создает анализатор зависимостей для TypeScript
    /// </summary>
    protected override IDependencyAnalyzer CreateDependencyAnalyzer()
    {
        return new Services.TypeScriptDependencyAnalyzer(NamingConverter);
    }

    /// <summary>
    /// Создает построитель TypeInfo для TypeScript
    /// </summary>
    protected override ITypeInfoBuilder CreateTypeInfoBuilder(ISchemaResolver schemaResolver)
    {
        return new Services.TypeScriptTypeInfoBuilder(TypeMapper, schemaResolver);
    }

    /// <summary>
    /// Создает извлекатель enum для TypeScript
    /// </summary>
    protected override IEnumExtractor CreateEnumExtractor(ISchemaResolver schemaResolver)
    {
        return new Services.TypeScriptEnumExtractor(schemaResolver, NamingConverter);
    }

    /// <summary>
    /// Создает стратегию именования для операций TypeScript
    /// </summary>
    protected override IOperationNamingStrategy CreateOperationNamingStrategy()
    {
        return new Services.TypeScriptOperationNamingStrategy();
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

            Logger.LogInformation("Starting TypeScript code generation using IR → AST → Code pipeline");

            // Используем новый IR → AST → Code pipeline
            var generatedFiles = new Dictionary<string, string>();
            var generationErrors = new List<string>();
            var generationWarnings = new List<string>();

            try
            {
                var files = GenerateCodeFromIr(document, config);
                foreach (var file in files)
                {
                    generatedFiles[file.Key] = file.Value;
                }
            }
            catch (Exception ex)
            {
                HandleGenerationError(ex, "code generation", generationErrors, 0);
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
        IReadOnlyList<IEnumTemplateData> enumTemplateDataList,
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

                // Определяем, используем ли мы Zod (schema-first подход) или raw TypeScript interfaces
                var useZod = options.ValidationLibrary == ValidationLibrary.Zod && _zodModelRenderer != null;

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

                if (useZod)
                {
                    // Schema-first подход: генерируем Zod schema и model файл с infer типом
                    try
                    {
                        // Устанавливаем формат на ValidationSchema для генерации Zod схемы
                        var originalFormat = typedModelData.Format;
                        typedModelData.Format = ValIO.Core.Models.TemplateData.ModelFormat.ValidationSchema;

                        // Генерируем Zod схему, передавая document и schemaResolver для создания правильной модели
                        var schemaCode = _zodModelRenderer.Render(typedModelData, document, schemaResolver);
                        var schemaFileName = $"{NamingConverter.ToCamelCase(typedModelData.Name)}.schema.ts";
                        var schemaFilePath = ResolveFilePath(schemaFileName, config, FileCategory.Models);
                        files[schemaFilePath] = schemaCode;

                        // Генерируем model файл с infer типом из схемы
                        var modelCode = GenerateZodInferTypeModel(typedModelData);
                        var modelFileName = $"{NamingConverter.ToCamelCase(typedModelData.Name)}.model.ts";
                        var modelFilePath = ResolveFilePath(modelFileName, config, FileCategory.Models);
                        files[modelFilePath] = modelCode;

                        // Восстанавливаем оригинальный формат
                        typedModelData.Format = originalFormat;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to generate Zod schema for model: {ModelName}", modelTemplateData.Name);
                        // Продолжаем генерацию других моделей
                    }
                }
                else
                {
                    // Raw TypeScript подход: генерируем только TypeScript interfaces
                    var modelCode = interfaceModelRenderer.Render(typedModelData);
                    var modelFileName = $"{NamingConverter.ToCamelCase(modelTemplateData.Name)}.model.ts";
                    var modelFilePath = ResolveFilePath(modelFileName, config, FileCategory.Models);
                    files[modelFilePath] = modelCode;
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

                // Определяем, используем ли мы Zod (schema-first подход) или raw TypeScript enums
                var useZodEnum = options.ValidationLibrary == ValidationLibrary.Zod && _zodEnumRenderer != null;

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

                if (useZodEnum)
                {
                    // Schema-first подход: генерируем Zod enum schema и enum файл с infer типом
                    try
                    {
                        // Устанавливаем формат на ValidationSchema для генерации Zod схемы
                        var originalFormat = typedEnumData.Format;
                        typedEnumData.Format = ValIO.Core.Models.TemplateData.EnumFormat.ValidationSchema;

                        // Генерируем Zod enum схему
                        var schemaCode = _zodEnumRenderer.Render(typedEnumData);
                        var schemaFileName = $"{NamingConverter.ToCamelCase(typedEnumData.Name)}.schema.ts";
                        var schemaFilePath = ResolveFilePath(schemaFileName, config, FileCategory.Models);
                        files[schemaFilePath] = schemaCode;

                        // Генерируем enum файл с infer типом из схемы
                        var enumCode = GenerateZodInferTypeEnum(typedEnumData);
                        var enumFileName = $"{NamingConverter.ToCamelCase(typedEnumData.Name)}.enum.ts";
                        var enumFilePath = ResolveFilePath(enumFileName, config, FileCategory.Models);
                        files[enumFilePath] = enumCode;

                        // Восстанавливаем оригинальный формат
                        typedEnumData.Format = originalFormat;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to generate Zod schema for enum: {EnumName}", typedEnumData.Name);
                        // Продолжаем генерацию других enum'ов
                    }
                }
                else
                {
                    // Raw TypeScript подход: генерируем только TypeScript enum/union type
                    IEnumFormatRenderer enumRenderer = typedEnumData.Format switch
                    {
                        ValIO.Core.Models.TemplateData.EnumFormat.Union => typeScriptUnionTypeRenderer,
                        _ => typeScriptEnumRenderer
                    };

                    var enumCode = enumRenderer.Render(typedEnumData);
                    var enumFileName = $"{NamingConverter.ToCamelCase(typedEnumData.Name)}.enum.ts";
                    var enumFilePath = ResolveFilePath(enumFileName, config, FileCategory.Models);
                    files[enumFilePath] = enumCode;
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


    /// <summary>
    /// Собирает все используемые типы из операций для генерации импортов
    /// </summary>
    // Методы CollectUsedTypes, CollectTypesFromTypeInfo, CreateTypeScriptOperationTemplateData, DetermineReturnTypeInfo, FindSchemaInContent удалены
    // - они больше не используются, так как операции теперь извлекаются через extractors в виде OperationTemplateData

    // Методы ValidateModelTemplateData и ValidateOperationTemplateData теперь в BaseCodeGenerator

    /// <summary>
    /// Получает имя файла модели из имени типа (внутренний метод)
    /// Используется в DetermineFileName для генерации имен файлов
    /// </summary>
    private string GetModelFileNameInternal(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;
        
        // Преобразуем PascalCase в camelCase для имени файла
        return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
    }

    // Метод EnsureTemplatesPreloadedAsync удален - шаблоны больше не используются
    
    // ============================================================================
    // Новые методы с использованием IR → AST → Code pipeline
    // ============================================================================
    // Эти методы используют новую архитектуру:
    // 1. OpenAPI Document → IrBuilder → IR (Language-agnostic Intermediate Representation)
    // 2. IR → AstBuilder → AST (Language-specific Abstract Syntax Tree)
    // 3. AST → AstRenderer → Code (String)
    //
    // Преимущества нового подхода:
    // - Полное разделение ответственности (IR, AST, Rendering)
    // - Возможность трансформаций AST перед рендерингом
    // - Легкое добавление новых языков и стеков
    // - Строгая типизация вместо работы со строками
    // ============================================================================
    
    /// <summary>
    /// Генерирует код используя новый IR → AST → Code pipeline
    /// </summary>
    /// <param name="document">OpenAPI документ</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <returns>Словарь с путями к файлам и их содержимым</returns>
    /// <remarks>
    /// Этот метод использует новую архитектуру генерации кода:
    /// 1. OpenAPI → IR (IrBuilder) - создает language-agnostic представление
    /// 2. IR → AST (TypeScriptAstBuilder) - создает TypeScript специфичное AST
    /// 3. AST → Code (TypeScriptAstRenderer) - рендерит AST в строку кода
    /// 
    /// В будущем этот метод должен заменить старый подход с TemplateData
    /// </remarks>
    private Dictionary<string, string> GenerateCodeFromIr(OpenApiDocument document, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();
        
        // 1. OpenAPI → IR
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, NamingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);
        
        // 2. IR → AST
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper);
        var sourceFiles = astBuilder.BuildSourceFiles(irDocument, config.ImportPath);
        
        // 3. AST → Code
        var astRenderer = new TypeScriptAstRenderer();
        
        foreach (var sourceFile in sourceFiles)
        {
            var code = astRenderer.Render(sourceFile);
            
            // Определяем имя файла на основе содержимого SourceFile
            var fileName = DetermineFileName(sourceFile, config);
            var filePath = ResolveFilePath(fileName, config, FileCategory.Models);
            
            files[filePath] = code;
        }
        
        return files;
    }
    
    /// <summary>
    /// Определяет имя файла на основе SourceFile
    /// </summary>
    /// <param name="sourceFile">AST узел SourceFile</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <returns>Имя файла с расширением .ts</returns>
    /// <remarks>
    /// Ищет первую декларацию (интерфейс, type alias или функцию) для определения имени файла
    /// </remarks>
    private string DetermineFileName(SourceFileNode sourceFile, GenerationConfiguration config)
    {
        // Ищем первую декларацию для определения имени файла
        foreach (var declaration in sourceFile.Declarations)
        {
            if (declaration is InterfaceDeclarationNode interfaceNode)
            {
                return $"{GetModelFileNameInternal(interfaceNode.Name.Name)}.ts";
            }
            if (declaration is TypeAliasDeclarationNode typeAliasNode)
            {
                return $"{GetModelFileNameInternal(typeAliasNode.Name.Name)}.ts";
            }
            if (declaration is FunctionDeclarationNode functionNode)
            {
                return $"{GetModelFileNameInternal(functionNode.Name.Name)}.ts";
            }
        }
        
        return "generated.ts";
    }
}
