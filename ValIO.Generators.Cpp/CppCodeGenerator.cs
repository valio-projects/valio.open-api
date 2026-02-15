using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp;

/// <summary>
/// Генератор C++ кода для Unreal Engine.
/// Реализует pipeline: OpenAPI → IR → C++/UE AST → Code (.h/.cpp).
/// Генерирует Blueprint-совместимые HTTP клиенты с USTRUCT моделями,
/// TFuture нативными методами, UValIOAsyncActionBase обёртками и PathsLibrary.
/// </summary>
public sealed class CppCodeGenerator : BaseCodeGenerator
{
    public CppCodeGenerator(
        ITypeMapper typeMapper,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<CppCodeGenerator> logger)
        : base(typeMapper, namingConverter, fileWriter, logger, GenerationLanguage.Cpp)
    {
    }

    public override bool SupportsLanguage(GenerationLanguage language)
    {
        return language == GenerationLanguage.Cpp;
    }

    public override bool SupportsOptions(GenerationOptions? options)
    {
        return true; // Мы пока принимаем любые опции
    }

    /// <summary>
    /// Основной метод генерации C++ кода для Unreal Engine.
    /// Pipeline: OpenAPI Document → IrBuilder → IR → CppAstBuilder → AST → CppAstRenderer → Code
    /// </summary>
    public override async Task<GenerationResult> GenerateAsync(
        OpenApiDocument document,
        GenerationConfiguration config)
    {
        var result = new GenerationResult { Success = false };
        var errors = ValidateInputs(document, config);

        if (errors.Count > 0)
        {
            result.Errors = errors;
            return result;
        }

        try
        {
            var generatedFiles = GenerateCodeFromIr(document, config);

            result.Success = true;
            result.GeneratedFiles = generatedFiles;

            await WriteGeneratedFilesAsync(generatedFiles, result);

            Logger.LogInformation(
                "C++ UE code generation completed. Generated {FileCount} files.",
                generatedFiles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "C++ UE code generation failed");
            result.Errors = new List<string> { $"Code generation failed: {ex.Message}" };
        }

        return result;
    }

    /// <summary>
    /// Генерация кода через IR → AST → Code pipeline
    /// </summary>
    private Dictionary<string, string> GenerateCodeFromIr(
        OpenApiDocument document,
        GenerationConfiguration config)
    {
        // Извлекаем опции C++
        var cppOptions = ExtractCppOptions(config);

        // 1. OpenAPI → IR
        var schemaResolver = new Core.Services.SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, NamingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);

        Logger.LogInformation(
            "IR built: {ModelCount} models, {EnumCount} enums, {ClientCount} clients",
            irDocument.Models.Count,
            irDocument.Enums.Count,
            irDocument.Clients.Count);

        // 2. IR → AST
        var astBuilder = new CppAstBuilder(cppOptions);
        var buildResult = astBuilder.Build(irDocument);

        foreach (var warning in buildResult.Warnings)
            Logger.LogWarning("{Warning}", warning);

        Logger.LogInformation("AST built: {FileCount} files", buildResult.Files.Count);

        // 3. AST → Code
        var renderer = new CppAstRenderer();
        var generatedFiles = new Dictionary<string, string>();

        foreach (var (filePath, astNode) in buildResult.Files)
        {
            var code = renderer.Render(astNode);
            var fullPath = Path.Combine(config.OutputPath, filePath);
            generatedFiles[fullPath] = code;

            Logger.LogDebug("Generated: {FilePath} ({Length} chars)", filePath, code.Length);
        }

        return generatedFiles;
    }

    /// <summary>
    /// Извлекает C++ опции из конфигурации генерации
    /// </summary>
    private static CppGenerationOptions ExtractCppOptions(GenerationConfiguration config)
    {
        // Если в опциях уже есть CppGenerationOptions — используем их
        if (config.Options?.CppOptions is CppGenerationOptions cppOptions)
        {
            cppOptions.OutputDirectory = config.OutputPath;
            if (string.IsNullOrEmpty(cppOptions.BlueprintCategory) && !string.IsNullOrEmpty(config.Namespace))
                cppOptions.BlueprintCategory = config.Namespace;
            return cppOptions;
        }

        return new CppGenerationOptions
        {
            OutputDirectory = config.OutputPath,
            BlueprintCategory = config.Namespace ?? "ValIO"
        };
    }
}
