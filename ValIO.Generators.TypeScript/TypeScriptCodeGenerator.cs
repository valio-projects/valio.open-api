using System;
using System.Collections.Immutable;
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
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Zod;
using ValIO.Generators.TypeScript.TanStackQuery;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Базовый генератор кода для TypeScript
/// </summary>
public class TypeScriptCodeGenerator : BaseCodeGenerator
{
    private readonly TypeScriptTypeMapper _typescriptTypeMapper;
    private readonly ILoggerFactory _loggerFactory;

    public TypeScriptCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<TypeScriptCodeGenerator> logger,
        ILoggerFactory loggerFactory)
        : base(typeMapper, namingConverter, fileWriter, logger, GenerationLanguage.TypeScript)
    {
        _typescriptTypeMapper = typeMapper as TypeScriptTypeMapper ?? throw new InvalidOperationException("TypeMapper must be TypeScriptTypeMapper");
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
                var files = GenerateCodeFromIr(document, config, generationWarnings);
                foreach (var file in files)
                {
                    generatedFiles[file.Key] = file.Value;
                }
            }
            catch (Exception ex)
            {
                HandleGenerationError(ex, "code generation", generationErrors, 0);
            }

            // Добавляем ошибки и предупреждения генерации
            if (generationErrors.Count > 0)
            {
                var errors = result.Errors.ToList();
                errors.AddRange(generationErrors);
                result.Errors = errors;
            }
            if (generationWarnings.Count > 0)
            {
                var warnings = result.Warnings.ToList();
                warnings.AddRange(generationWarnings);
                result.Warnings = warnings;
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
    // Метод GenerateModelsAndEnumsUnifiedAsync удален - теперь используется IR → AST → Code pipeline


    // Методы GroupOperations и ExtractRouteGroup удалены - они больше не используются в новом IR → AST → Code pipeline
    // Группировка операций теперь происходит на уровне IR (IrClient) в IrBuilder


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
    /// Поддерживает:
    /// - Генерацию моделей (interfaces/types)
    /// - Генерацию enum'ов
    /// - Генерацию HTTP клиентов (Axios/Fetch)
    /// - Генерацию Zod схем (если ValidationLibrary = Zod)
    /// - Генерацию TanStack Query hooks (если UseTanStackQuery = true)
    /// </remarks>
    private Dictionary<string, string> GenerateCodeFromIr(OpenApiDocument document, GenerationConfiguration config, ICollection<string> warnings)
    {
        var files = new Dictionary<string, string>();
        
        // Получаем TypeScript опции
        var tsOptions = GetTypeScriptOptions(config);
        
        // 1. OpenAPI → IR
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, NamingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document, warnings);
        
        var httpTransports = tsOptions.HttpTransports;

        // 2. IR → AST для моделей и enum'ов (без endpoint-файлов — они генерируются с суффиксами .axios / .fetch)
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        var sourceFiles = astBuilder.BuildSourceFiles(irDocument, importPath: null, fileOrganization: config.FileOrganization, includeOperationEndpointFiles: false);
        
        // 3. AST → Code для моделей и enum'ов
        var astRenderer = new TypeScriptAstRenderer();
        
        foreach (var sourceFile in sourceFiles)
        {
            var code = astRenderer.Render(sourceFile);
            
            // Определяем имя файла и категорию на основе содержимого SourceFile
            var (fileName, fileCategory) = DetermineFileNameAndCategory(sourceFile, config);
            var filePath = ResolveFilePath(fileName, config, fileCategory);
            
            files[filePath] = code;
        }

        GenerateEndpointTransportClientFiles(files, irDocument, config, tsOptions, astBuilder, astRenderer, httpTransports);

        if (httpTransports.HasFlag(TypeScriptHttpTransports.AngularRxjs))
        {
            var angularFiles = GenerateAngularServiceFiles(irDocument, config, tsOptions);
            foreach (var file in angularFiles)
            {
                files[file.Key] = file.Value;
            }
        }

        // 4. Генерация params файлов (стиль params для axios и/или fetch и/или angular-rxjs)
        if (httpTransports.HasFlag(TypeScriptHttpTransports.Axios)
            || httpTransports.HasFlag(TypeScriptHttpTransports.Fetch)
            || httpTransports.HasFlag(TypeScriptHttpTransports.AngularRxjs))
        {
            var paramsFiles = GenerateParamsFiles(irDocument, config, tsOptions);
            foreach (var file in paramsFiles)
            {
                files[file.Key] = file.Value;
            }
        }

        // 4b. Генерация results файлов (discriminated union по status codes)
        if (httpTransports.HasFlag(TypeScriptHttpTransports.Axios)
            || httpTransports.HasFlag(TypeScriptHttpTransports.Fetch)
            || httpTransports.HasFlag(TypeScriptHttpTransports.AngularRxjs)
            || tsOptions.UseTanStackQuery)
        {
            var resultsFiles = GenerateResultsFiles(irDocument, config, tsOptions);
            foreach (var file in resultsFiles)
            {
                files[file.Key] = file.Value;
            }
        }

        // 5. Генерация Zod схем (если ValidationLibrary = Zod)
        if (tsOptions.ValidationLibrary == ValidationLibrary.Zod)
        {
            var zodFiles = GenerateZodSchemas(irDocument, config, tsOptions);
            foreach (var file in zodFiles)
            {
                files[file.Key] = file.Value;
            }

            if (httpTransports.HasFlag(TypeScriptHttpTransports.Axios) || httpTransports.HasFlag(TypeScriptHttpTransports.Fetch))
            {
                var paramsSchemaFiles = GenerateParamsSchemaFiles(irDocument, config, tsOptions);
                foreach (var file in paramsSchemaFiles)
                {
                    files[file.Key] = file.Value;
                }
            }
        }
        
        // 6. Генерация TanStack Query hooks (если UseTanStackQuery = true)
        if (tsOptions.UseTanStackQuery)
        {
            var queryFiles = GenerateTanStackQueryHooks(irDocument, config, tsOptions);
            foreach (var file in queryFiles)
            {
                files[file.Key] = file.Value;
            }
        }
        
        return files;
    }

    /// <summary>
    /// Генерирует endpoint-клиенты <c>{op}.axios.ts</c> и/или <c>{op}.fetch.ts</c>.
    /// </summary>
    private void GenerateEndpointTransportClientFiles(
        Dictionary<string, string> files,
        IrDocument irDocument,
        GenerationConfiguration config,
        TypeScriptGenerationOptions tsOptions,
        TypeScriptAstBuilder astBuilder,
        TypeScriptAstRenderer astRenderer,
        TypeScriptHttpTransports httpTransports)
    {
        foreach (var client in irDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                var operationFileName = GetModelFileNameInternal(operation.MethodName);

                if (httpTransports.HasFlag(TypeScriptHttpTransports.Axios))
                {
                    var axiosSource = astBuilder.BuildSourceFileForOperation(
                        operation,
                        importPath: null,
                        config.FileOrganization,
                        TypeScriptEndpointTransport.Axios);
                    var axiosCode = astRenderer.Render(axiosSource);
                    var axiosPath = ResolveFilePath($"{operationFileName}/{operationFileName}.axios.ts", config, FileCategory.Clients);
                    files[axiosPath] = axiosCode;
                }

                if (httpTransports.HasFlag(TypeScriptHttpTransports.Fetch))
                {
                    var fetchSource = astBuilder.BuildSourceFileForOperation(
                        operation,
                        importPath: null,
                        config.FileOrganization,
                        TypeScriptEndpointTransport.Fetch);
                    var fetchCode = astRenderer.Render(fetchSource);
                    var fetchPath = ResolveFilePath($"{operationFileName}/{operationFileName}.fetch.ts", config, FileCategory.Clients);
                    files[fetchPath] = fetchCode;
                }
            }
        }
    }

    /// <summary>
    /// Генерирует Angular RxJS HttpClient сервисы: один <c>{ClientName}Service.service.ts</c>
    /// на каждый <see cref="IrClient"/> (один класс с одним методом на операцию).
    /// </summary>
    private Dictionary<string, string> GenerateAngularServiceFiles(
        IrDocument irDocument,
        GenerationConfiguration config,
        TypeScriptGenerationOptions tsOptions)
    {
        var files = new Dictionary<string, string>();
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder =
            irType => astBuilder.BuildType(irType);
        var serviceBuilder = new Angular.Ast.Builders.AngularServiceAstBuilder(NamingConverter, typeBuilder, tsOptions);
        var astRenderer = new Ast.Renderers.TypeScriptAstRenderer();

        foreach (var client in irDocument.Clients)
        {
            var sourceFile = serviceBuilder.BuildServiceSourceFile(client, config.FileOrganization);
            var code = astRenderer.Render(sourceFile);
            var fileName = $"{serviceBuilder.ServiceFileName(client)}.service.ts";
            var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);
            files[filePath] = code;
        }

        return files;
    }

    /// <summary>
    /// Генерирует results файлы (discriminated union по status codes) для операций
    /// </summary>
    private Dictionary<string, string> GenerateResultsFiles(
        IrDocument irDocument,
        GenerationConfiguration config,
        TypeScriptGenerationOptions tsOptions)
    {
        var files = new Dictionary<string, string>();

        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder =
            irType => astBuilder.BuildType(irType);
        var resultsAstBuilder = new Ast.Builders.TypeScriptResultsAstBuilder(NamingConverter, typeBuilder);
        var astRenderer = new Ast.Renderers.TypeScriptAstRenderer();

        foreach (var client in irDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                if (!resultsAstBuilder.HasResponsesWithBody(operation))
                    continue;

                var resultsSourceFile = resultsAstBuilder.BuildResultsSourceFile(
                    operation,
                    importPath: null,
                    config.FileOrganization,
                    operation.MethodName);

                var code = astRenderer.Render(resultsSourceFile);
                var operationFileName = GetModelFileNameInternal(operation.MethodName);
                var fileName = $"{operationFileName}/{operationFileName}.results.ts";
                var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);
                files[filePath] = code;
            }
        }

        return files;
    }

    /// <summary>
    /// Генерирует Zod схемы из IR документа
    /// </summary>
    /// <param name="irDocument">IR документ</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <param name="tsOptions">TypeScript опции</param>
    /// <returns>Словарь с путями к файлам и их содержимым</returns>
    private Dictionary<string, string> GenerateZodSchemas(
        IrDocument irDocument, 
        GenerationConfiguration config, 
        TypeScriptGenerationOptions tsOptions)
    {
        var files = new Dictionary<string, string>();
        
        // Создаем TypeScriptAstBuilder для построения TypeNode из IrType
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        
        // Создаем typeBuilder для ZodAstBuilder
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder = 
            irType => astBuilder.BuildType(irType);
        
        // Создаем новый ZodSchemaRenderer с typeBuilder
        var zodRendererWithBuilder = new ZodSchemaRenderer(
            NamingConverter,
            typeBuilder,
            tsOptions.UseBigInt);
        
        // Схемы помещаются в папку модели: models/modelName/modelName.schema.ts
        var modelsBasePath = config.FileOrganization.ModelsPath ?? "models";
        foreach (var model in irDocument.Models)
        {
            var modelFileName = GetModelFileNameInternal(model.Name);
            var fileName = $"{modelFileName}/{modelFileName}.schema.ts";
            var code = zodRendererWithBuilder.RenderFromIr(model, importPath: null, modelFileName, modelsBasePath);
            var filePath = ResolveFilePath(fileName, config, FileCategory.Models);
            files[filePath] = code;
        }
        
        foreach (var enumModel in irDocument.Enums)
        {
            var modelFileName = GetModelFileNameInternal(enumModel.Name);
            var fileName = $"{modelFileName}/{modelFileName}.schema.ts";
            var code = zodRendererWithBuilder.RenderEnumFromIr(enumModel, importPath: null, modelFileName, modelsBasePath);
            var filePath = ResolveFilePath(fileName, config, FileCategory.Models);
            files[filePath] = code;
        }
        
        return files;
    }

    /// <summary>
    /// Генерирует params файлы (интерфейсы параметров) для операций при использовании Axios
    /// </summary>
    private Dictionary<string, string> GenerateParamsFiles(
        IrDocument irDocument,
        GenerationConfiguration config,
        TypeScriptGenerationOptions tsOptions)
    {
        var files = new Dictionary<string, string>();

        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder =
            irType => astBuilder.BuildType(irType);
        var paramsAstBuilder = new Ast.Builders.TypeScriptParamsAstBuilder(NamingConverter, typeBuilder);
        var astRenderer = new Ast.Renderers.TypeScriptAstRenderer();

        foreach (var client in irDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                if (!paramsAstBuilder.HasParameters(operation))
                    continue;

                var paramsSourceFile = paramsAstBuilder.BuildParamsSourceFile(
                    operation,
                    importPath: null,
                    config.FileOrganization,
                    operation.MethodName);

                var code = astRenderer.Render(paramsSourceFile);
                var operationFileName = GetModelFileNameInternal(operation.MethodName);
                var fileName = $"{operationFileName}/{operationFileName}.params.ts";
                var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);
                files[filePath] = code;
            }
        }

        return files;
    }

    /// <summary>
    /// Генерирует Zod схемы для params операций
    /// </summary>
    private Dictionary<string, string> GenerateParamsSchemaFiles(
        IrDocument irDocument,
        GenerationConfiguration config,
        TypeScriptGenerationOptions tsOptions)
    {
        var files = new Dictionary<string, string>();
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder =
            irType => astBuilder.BuildType(irType);
        var zodRenderer = new ZodSchemaRenderer(NamingConverter, typeBuilder, tsOptions.UseBigInt);

        foreach (var client in irDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                var hasParams = operation.PathParameters.Count > 0 || operation.QueryParameters.Count > 0;
                if (!hasParams)
                    continue;

                var code = zodRenderer.RenderParamsSchemasFromOperation(operation);
                var operationFileName = GetModelFileNameInternal(operation.MethodName);
                var fileName = $"{operationFileName}/{operationFileName}.params.schema.ts";
                var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);
                files[filePath] = code;
            }
        }

        return files;
    }
    
    /// <summary>
    /// Генерирует TanStack Query hooks из IR документа
    /// </summary>
    /// <param name="irDocument">IR документ</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <param name="tsOptions">TypeScript опции</param>
    /// <returns>Словарь с путями к файлам и их содержимым</returns>
    private Dictionary<string, string> GenerateTanStackQueryHooks(
        IrDocument irDocument,
        GenerationConfiguration config,
        TypeScriptGenerationOptions tsOptions)
    {
        var files = new Dictionary<string, string>();
        
        // Создаем typeBuilder для TanStackQueryAstBuilder
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder = 
            irType => astBuilder.BuildType(irType);
        
        var hookUsesAxios = TypeScriptHttpTransportsTanStack.PreferredHookTransport(tsOptions.HttpTransports) == TypeScriptEndpointTransport.Axios;
        var hookAstBuilder = new TanStackQuery.Ast.Builders.TanStackQueryHookAstBuilder(NamingConverter, typeBuilder, hookUsesAxios);
        var tsRenderer = new Ast.Renderers.TypeScriptAstRenderer();

        foreach (var client in irDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                var isMutation = operation.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE";
                var operationFileName = GetModelFileNameInternal(operation.MethodName);

                var hookSourceFile = hookAstBuilder.BuildHookSourceFile(operation, operationFileName, isMutation);
                var fullCode = tsRenderer.Render(hookSourceFile);

                var fileName = $"{operationFileName}/{operationFileName}.hook.ts";
                var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);
                files[filePath] = fullCode;
            }
        }
        
        return files;
    }
    
    /// <summary>
    /// Строит вызов функции клиента для операции.
    /// При Axios передаёт params, иначе отдельные параметры.
    /// </summary>
    private ValIO.Core.Ast.Nodes.ExpressionNode BuildClientFunctionCall(
        IrOperation operation,
        TypeScriptAstBuilder astBuilder,
        TypeScriptGenerationOptions tsOptions)
    {
        var useParams = tsOptions.HttpTransports.HasFlag(TypeScriptHttpTransports.Axios)
            || tsOptions.HttpTransports.HasFlag(TypeScriptHttpTransports.Fetch);

        var functionName = new ValIO.Core.Ast.Nodes.IdentifierNode(operation.MethodName);
        var arguments = new List<ValIO.Core.Ast.Nodes.ExpressionNode>();

        if (useParams)
        {
            arguments.Add(new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(
                new ValIO.Core.Ast.Nodes.IdentifierNode("params")));
        }
        else
        {
            foreach (var param in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
            {
                arguments.Add(new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(
                    new ValIO.Core.Ast.Nodes.IdentifierNode(param.Name)));
            }
            if (operation.RequestBody != null)
            {
                arguments.Add(new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(
                    new ValIO.Core.Ast.Nodes.IdentifierNode("request")));
            }
        }

        return new ValIO.Core.Ast.Nodes.Expressions.CallExpressionNode(
            new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(functionName),
            arguments.ToImmutableList());
    }
    
    /// <summary>
    /// Определяет имя файла и категорию на основе SourceFile
    /// </summary>
    /// <param name="sourceFile">AST узел SourceFile</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <returns>Кортеж (имя файла с расширением .ts, категория файла)</returns>
    /// <remarks>
    /// Ищет первую декларацию (интерфейс, type alias или функцию) для определения имени файла и категории.
    /// Клиенты определяются по наличию функций (HTTP операций).
    /// </remarks>
    private (string fileName, FileCategory category) DetermineFileNameAndCategory(
        SourceFileNode sourceFile, 
        GenerationConfiguration config)
    {
        // Ищем первую декларацию для определения имени файла и категории
        foreach (var declaration in sourceFile.Declarations)
        {
            if (declaration is InterfaceDeclarationNode interfaceNode)
            {
                // Интерфейсы могут быть как моделями, так и клиентами
                // Проверяем наличие методов в интерфейсе (для клиентов)
                var isClient = interfaceNode.Members.Any(m => m is MethodDeclarationNode); // Проверяем наличие методов
                var category = isClient ? FileCategory.Clients : FileCategory.Models;
                var name = GetModelFileNameInternal(interfaceNode.Name.Name);
                return ($"{name}/{name}.ts", category);
            }
            if (declaration is TypeAliasDeclarationNode typeAliasNode)
            {
                var name = GetModelFileNameInternal(typeAliasNode.Name.Name);
                return ($"{name}/{name}.ts", FileCategory.Models);
            }
            if (declaration is FunctionDeclarationNode functionNode)
            {
                var name = GetModelFileNameInternal(functionNode.Name.Name);
                return ($"{name}/{name}.ts", FileCategory.Clients);
            }
        }
        
        return ("generated/generated.ts", FileCategory.Models);
    }
}
