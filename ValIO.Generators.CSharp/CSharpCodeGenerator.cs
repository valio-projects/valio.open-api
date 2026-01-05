using System;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using System.Linq;
using ValIO.Generators.CSharp.Models;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Генератор кода для C#
/// </summary>
public class CSharpCodeGenerator : BaseCodeGenerator
{
    private readonly CSharpTypeMapper _csharpTypeMapper;

    public CSharpCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<CSharpCodeGenerator> logger)
        : base(typeMapper, namingConverter, fileWriter, logger, GenerationLanguage.CSharp)
    {
        _csharpTypeMapper = typeMapper as CSharpTypeMapper ?? throw new InvalidOperationException("TypeMapper must be CSharpTypeMapper");
    }

    /// <summary>
    /// Создает анализатор зависимостей для C#
    /// </summary>
    protected override IDependencyAnalyzer CreateDependencyAnalyzer()
    {
        return new Services.CSharpDependencyAnalyzer(NamingConverter);
    }

    /// <summary>
    /// Создает построитель TypeInfo для C#
    /// </summary>
    protected override ITypeInfoBuilder CreateTypeInfoBuilder(ISchemaResolver schemaResolver)
    {
        return new Services.CSharpTypeInfoBuilder(TypeMapper, schemaResolver);
    }

    /// <summary>
    /// Создает извлекатель enum для C#
    /// </summary>
    protected override IEnumExtractor CreateEnumExtractor(ISchemaResolver schemaResolver)
    {
        return new Services.CSharpEnumExtractor(schemaResolver, NamingConverter);
    }

    /// <summary>
    /// Создает стратегию именования для операций C#
    /// </summary>
    protected override IOperationNamingStrategy CreateOperationNamingStrategy()
    {
        return new Services.CSharpOperationNamingStrategy();
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

            Logger.LogInformation("Starting C# code generation using IR → AST → Code pipeline");

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
    // Старые методы GenerateEnums, GenerateModels, GenerateClients удалены - теперь используется IR → AST → Code pipeline через GenerateCodeFromIr

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

    // Старые методы GenerateEnums, GenerateModels, GenerateClients, GenerateQueryParametersModelCode, GenerateExceptionCode, GenerateClientCode удалены
    // Теперь используется IR → AST → Code pipeline через GenerateCodeFromIr

    // GroupOperations и ExtractRouteGroup теперь в базовом классе BaseCodeGenerator
    // Переопределяем ExtractRouteGroup для более сложной логики C#

    /// <summary>
    /// Извлекает имя группы из пути операции (C# специфичная реализация)
    /// Например: /api/users/{id} -> Users, /api/products -> Products
    /// </summary>
    protected override string ExtractRouteGroup(string path)
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

    // ============================================================================
    // Новые методы с использованием IR → AST → Code pipeline
    // ============================================================================
    // Старые методы GenerateEnums, GenerateModels, GenerateClients, GenerateQueryParametersModelCode,
    // GenerateExceptionCode, GenerateClientCode, GenerateBaseExceptionCode, DecodeHtmlEntities,
    // FormatEnumValue, EscapeXmlComment, GenerateOperationId, FormatParameterString удалены.
    // Теперь используется IR → AST → Code pipeline через GenerateCodeFromIr
    // ============================================================================


    // Метод EnsureTemplatesPreloadedAsync удален - шаблоны больше не используются
    // Методы ValidateModelTemplateData и ValidateOperationTemplateData теперь в BaseCodeGenerator
    
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
    /// 2. IR → AST (CSharpAstBuilder) - создает C# специфичное AST
    /// 3. AST → Code (CSharpAstRenderer) - рендерит AST в строку кода
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
        var astBuilder = new CSharpAstBuilder(NamingConverter, TypeMapper);
        var compilationUnits = astBuilder.BuildCompilationUnits(irDocument, config.Namespace);
        
        // 3. AST → Code
        var astRenderer = new CSharpAstRenderer();
        
        foreach (var compilationUnit in compilationUnits)
        {
            var code = astRenderer.Render(compilationUnit);
            
            // Определяем имя файла на основе содержимого CompilationUnit
            var fileName = DetermineFileName(compilationUnit, config);
            var filePath = ResolveFilePath(fileName, config, FileCategory.Models);
            
            files[filePath] = code;
        }
        
        return files;
    }
    
    /// <summary>
    /// Определяет имя файла на основе CompilationUnit
    /// </summary>
    /// <param name="compilationUnit">AST узел CompilationUnit</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <returns>Имя файла с расширением .cs</returns>
    /// <remarks>
    /// Ищет первую декларацию (класс или enum) в namespace для определения имени файла
    /// </remarks>
    private string DetermineFileName(CompilationUnitNode compilationUnit, GenerationConfiguration config)
    {
        // Ищем первую декларацию в namespace для определения имени файла
        foreach (var namespaceNode in compilationUnit.Namespaces)
        {
            foreach (var declaration in namespaceNode.Declarations)
            {
                if (declaration is ClassDeclarationNode classNode)
                {
                    return $"{classNode.Name.Name}.cs";
                }
                if (declaration is EnumDeclarationNode enumNode)
                {
                    return $"{enumNode.Name.Name}.cs";
                }
            }
        }
        
        return "Generated.cs";
    }
}

