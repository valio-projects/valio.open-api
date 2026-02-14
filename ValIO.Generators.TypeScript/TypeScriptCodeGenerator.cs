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
    private Dictionary<string, string> GenerateCodeFromIr(OpenApiDocument document, GenerationConfiguration config)
    {
        var files = new Dictionary<string, string>();
        
        // Получаем TypeScript опции
        var tsOptions = GetTypeScriptOptions(config);
        
        // 1. OpenAPI → IR
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, NamingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);
        
        // 2. IR → AST для моделей и enum'ов
        var astBuilder = new TypeScriptAstBuilder(NamingConverter, TypeMapper, tsOptions);
        var sourceFiles = astBuilder.BuildSourceFiles(irDocument, importPath: null, fileOrganization: config.FileOrganization);
        
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
        
        // 4. Генерация Zod схем (если ValidationLibrary = Zod)
        if (tsOptions.ValidationLibrary == ValidationLibrary.Zod)
        {
            var zodFiles = GenerateZodSchemas(irDocument, config, tsOptions);
            foreach (var file in zodFiles)
            {
                files[file.Key] = file.Value;
            }
        }
        
        // 5. Генерация TanStack Query hooks (если UseTanStackQuery = true)
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
            typeBuilder);
        
        // Вычисляем путь к папке со схемами для вычисления относительных путей
        // Используем ResolveFilePath для получения полного пути, затем извлекаем путь к папке
        var schemasFilePath = ResolveFilePath("dummy.ts", config, FileCategory.Schemas);
        var schemasPath = "";
        if (!string.IsNullOrEmpty(schemasFilePath))
        {
            var lastSlash = Math.Max(schemasFilePath.LastIndexOf('/'), schemasFilePath.LastIndexOf('\\'));
            if (lastSlash >= 0)
            {
                schemasPath = schemasFilePath.Substring(0, lastSlash);
            }
        }
        
        // Генерируем Zod схемы для моделей
        foreach (var model in irDocument.Models)
        {
            var fileName = $"{GetModelFileNameInternal(model.Name)}.schema.ts";
            var currentFileName = GetModelFileNameInternal(model.Name);
            var code = zodRendererWithBuilder.RenderFromIr(model, importPath: null, currentFileName, schemasPath);
            var filePath = ResolveFilePath(fileName, config, FileCategory.Schemas);
            files[filePath] = code;
        }
        
        // Генерируем Zod схемы для enum'ов
        foreach (var enumModel in irDocument.Enums)
        {
            var fileName = $"{GetModelFileNameInternal(enumModel.Name)}.schema.ts";
            var currentFileName = GetModelFileNameInternal(enumModel.Name);
            var code = zodRendererWithBuilder.RenderEnumFromIr(enumModel, importPath: null, currentFileName, schemasPath);
            var filePath = ResolveFilePath(fileName, config, FileCategory.Schemas);
            files[filePath] = code;
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
        
        // Создаем TanStackQueryRenderer
        var queryRenderer = new TanStackQueryRenderer(
            NamingConverter,
            typeBuilder);
        
        // Генерируем hooks для каждой операции в каждом клиенте
        foreach (var client in irDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                // Определяем, является ли операция мутацией (POST, PUT, PATCH, DELETE)
                var isMutation = operation.HttpMethod is 
                    "POST" or "PUT" or "PATCH" or "DELETE";
                
                // Строим вызов функции клиента
                var clientFunction = BuildClientFunctionCall(operation, astBuilder);
                
                // Рендерим hook
                var hookCode = queryRenderer.RenderHook(operation, clientFunction, isMutation);
                
                // Рендерим query key функцию
                var queryKeyCode = queryRenderer.RenderQueryKeyFunction(operation);
                
                // Объединяем код
                var fullCode = new StringBuilder();
                fullCode.AppendLine("import { useQuery, useMutation } from '@tanstack/react-query';");
                fullCode.AppendLine();
                fullCode.Append(queryKeyCode);
                fullCode.AppendLine();
                fullCode.Append(hookCode);
                
                var fileName = $"{GetModelFileNameInternal(operation.MethodName)}.hook.ts";
                var filePath = ResolveFilePath(fileName, config, FileCategory.Clients);
                files[filePath] = fullCode.ToString();
            }
        }
        
        return files;
    }
    
    /// <summary>
    /// Строит вызов функции клиента для операции
    /// </summary>
    private ValIO.Core.Ast.Nodes.ExpressionNode BuildClientFunctionCall(
        IrOperation operation,
        TypeScriptAstBuilder astBuilder)
    {
        // Строим вызов функции клиента
        var functionName = new ValIO.Core.Ast.Nodes.IdentifierNode(operation.MethodName);
        var arguments = new List<ValIO.Core.Ast.Nodes.ExpressionNode>();
        
        // Добавляем параметры
        foreach (var param in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
        {
            arguments.Add(new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(
                new ValIO.Core.Ast.Nodes.IdentifierNode(param.Name)));
        }
        
        // Добавляем request body, если есть
        if (operation.RequestBody != null)
        {
            arguments.Add(new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(
                new ValIO.Core.Ast.Nodes.IdentifierNode("request")));
        }
        
        var functionCall = new ValIO.Core.Ast.Nodes.Expressions.CallExpressionNode(
            new ValIO.Core.Ast.Nodes.Expressions.IdentifierExpressionNode(functionName),
            arguments.ToImmutableList());
        
        return functionCall;
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
                return ($"{GetModelFileNameInternal(interfaceNode.Name.Name)}.ts", category);
            }
            if (declaration is TypeAliasDeclarationNode typeAliasNode)
            {
                return ($"{GetModelFileNameInternal(typeAliasNode.Name.Name)}.ts", FileCategory.Models);
            }
            if (declaration is FunctionDeclarationNode functionNode)
            {
                // Функции обычно являются частью клиентов
                return ($"{GetModelFileNameInternal(functionNode.Name.Name)}.ts", FileCategory.Clients);
            }
        }
        
        return ("generated.ts", FileCategory.Models);
    }
}
