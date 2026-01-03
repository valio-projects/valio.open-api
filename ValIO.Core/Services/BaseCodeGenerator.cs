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
        OperationCache = new Dictionary<string, OpenApiOperation>();
        SchemaTypeInfoCache = new Dictionary<string, TypeInfo>();
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
}
