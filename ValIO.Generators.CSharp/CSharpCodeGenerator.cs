using System;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using System.Linq;
using ValIO.Core.Ast.Nodes;
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
    public CSharpCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<CSharpCodeGenerator> logger)
        : base(typeMapper, namingConverter, fileWriter, logger, GenerationLanguage.CSharp)
    {
    }

    // Методы CreateDependencyAnalyzer, CreateTypeInfoBuilder, CreateEnumExtractor, CreateOperationNamingStrategy
    // удалены - они больше не используются, так как новый IR → AST → Code pipeline не использует TemplateData подход

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

    // Методы BuildOperationCache, GetCSharpOptions, ExtractRouteGroup удалены
    // Они использовались только в старом TemplateData подходе
    // Новый IR → AST → Code pipeline не использует эти методы

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
        // Используем CSharpNamingConverter для обработки зарезервированных ключевых слов на этапе IR
        var csharpNamingConverter = new Services.CSharpNamingConverter();
        var irBuilder = new IrBuilder(schemaResolver, csharpNamingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);
        
            // 2. IR → AST
            var csharpOptions = GetCSharpOptions(config.Options);
            var astBuilder = new CSharpAstBuilder(NamingConverter, csharpOptions);
            var compilationUnits = astBuilder.BuildCompilationUnits(irDocument, config.Namespace);
        
        // 3. AST → Code
        var astRenderer = new CSharpAstRenderer();
        
        foreach (var compilationUnit in compilationUnits)
        {
            var code = astRenderer.Render(compilationUnit);
            
            // Определяем имя файла и категорию на основе содержимого CompilationUnit
            var (fileName, fileCategory, isInterface) = DetermineFileNameAndCategory(compilationUnit, config);
            var filePath = ResolveFilePath(fileName, config, fileCategory);
            
            files[filePath] = code;
        }
        
        return files;
    }
    
    /// <summary>
    /// Получает C# опции из конфигурации
    /// </summary>
    private CSharpGenerationOptions GetCSharpOptions(GenerationOptions? options)
    {
        if (options?.CSharpOptions is CSharpGenerationOptions csharpOptions)
        {
            return csharpOptions;
        }
        
        return new CSharpGenerationOptions();
    }
    
    /// <summary>
    /// Определяет имя файла и категорию на основе CompilationUnit
    /// </summary>
    /// <param name="compilationUnit">AST узел CompilationUnit</param>
    /// <param name="config">Конфигурация генерации</param>
    /// <returns>Кортеж (имя файла с расширением .cs, категория файла, является ли интерфейсом)</returns>
    /// <remarks>
    /// Ищет первую декларацию (класс, интерфейс или enum) в namespace для определения имени файла и категории.
    /// Клиенты определяются по наличию методов HTTP операций.
    /// </remarks>
    private (string fileName, FileCategory category, bool isInterface) DetermineFileNameAndCategory(CompilationUnitNode compilationUnit, GenerationConfiguration config)
    {
        // Ищем первую декларацию в namespace для определения имени файла и категории
        foreach (var namespaceNode in compilationUnit.Namespaces)
        {
            foreach (var declaration in namespaceNode.Declarations)
            {
                if (declaration is InterfaceDeclarationNode interfaceNode)
                {
                    // Интерфейсы для клиентов
                    var category = FileCategory.Clients;
                    return ($"{interfaceNode.Name.Name}.cs", category, true);
                }
                if (declaration is ClassDeclarationNode classNode)
                {
                    // Проверяем, является ли это HTTP клиентом (есть методы с async)
                    var isClient = classNode.Members.Any(m => 
                        m is MethodDeclarationNode method && method.IsAsync);
                    
                    var category = isClient ? FileCategory.Clients : FileCategory.Models;
                    return ($"{classNode.Name.Name}.cs", category, false);
                }
                if (declaration is EnumDeclarationNode enumNode)
                {
                    return ($"{enumNode.Name.Name}.cs", FileCategory.Models, false);
                }
            }
        }
        
        return ("Generated.cs", FileCategory.Models, false);
    }
}

