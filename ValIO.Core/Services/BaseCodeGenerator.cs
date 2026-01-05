using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

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
    
    // OperationCache и SchemaTypeInfoCache удалены - они использовались только в старом TemplateData подходе
    // Новый IR → AST → Code pipeline не использует эти кэши

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

    // Методы CreateDependencyAnalyzer, CreateTypeInfoBuilder, CreateEnumExtractor, CreateOperationNamingStrategy
    // удалены - они использовались только в старом TemplateData подходе
    // Новый IR → AST → Code pipeline не использует эти методы

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

    // Методы BuildOperationCache и InitializeCaches удалены
    // Они использовались только в старом TemplateData подходе
    // Новый IR → AST → Code pipeline не использует эти кэши

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

    // Методы GroupOperations и ExtractRouteGroup удалены
    // Они использовались только в старом TemplateData подходе
    // Новый IR → AST → Code pipeline использует IrDocument для группировки операций

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

    // Класс ExtractedData и методы ExtractData, ValidateExtractedData, ValidateModelTemplateData, ValidateOperationTemplateData
    // удалены - они использовались только в старом TemplateData подходе
    // Новый IR → AST → Code pipeline не использует эти методы
}
