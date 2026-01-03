using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Опции генерации TypeScript кода
/// </summary>
public class TypeScriptGenerationOptions
{
    /// <summary>
    /// Формат моделей: Interfaces или ValidationSchemas
    /// </summary>
    public TypeScriptModelFormat ModelFormat { get; set; } = TypeScriptModelFormat.Interfaces;
    
    /// <summary>
    /// Библиотека валидации (если ModelFormat = ValidationSchemas)
    /// </summary>
    public ValidationLibrary? ValidationLibrary { get; set; }
    
    /// <summary>
    /// HTTP клиент: Fetch или Axios
    /// </summary>
    public HttpClientLibrary HttpClientLibrary { get; set; } = HttpClientLibrary.Fetch;
    
    /// <summary>
    /// Использовать TanStack Query
    /// </summary>
    public bool UseTanStackQuery { get; set; } = false;
    
    /// <summary>
    /// Сериализовать/валидировать payload через схемы (только если ValidationLibrary задан)
    /// </summary>
    public bool SerializeWithSchemas { get; set; } = true;
    
    /// <summary>
    /// Десериализовать/валидировать response через схемы (только если ValidationLibrary задан)
    /// </summary>
    public bool DeserializeWithSchemas { get; set; } = true;
    
    /// <summary>
    /// Генерировать enum'ы как TypeScript enum или union types
    /// </summary>
    public TypeScriptEnumFormat EnumFormat { get; set; } = TypeScriptEnumFormat.Enum;
    
    /// <summary>
    /// Использовать strict null checks
    /// </summary>
    public bool UseStrictNullChecks { get; set; } = true;
    
    // QueryParametersStyle не используется для TypeScript, query параметры всегда передаются как отдельные параметры
    // Это свойство оставлено для обратной совместимости, но не используется
    
    /// <summary>
    /// Настройки для Axios HTTP клиента
    /// </summary>
    public AxiosOptions? Axios { get; set; }
}

/// <summary>
/// Настройки для Axios HTTP клиента
/// </summary>
public class AxiosOptions
{
    /// <summary>
    /// Путь до файла с custom axios instance (например, '../mutator/custom-instance')
    /// </summary>
    public string? CustomInstancePath { get; set; }
    
    /// <summary>
    /// Название константы с custom axios instance (например, 'customInstance')
    /// </summary>
    public string? CustomInstanceName { get; set; }
}

/// <summary>
/// Формат моделей для генерации TypeScript
/// </summary>
public enum TypeScriptModelFormat
{
    /// <summary>
    /// TypeScript interfaces
    /// </summary>
    Interfaces,
    
    /// <summary>
    /// Zod/Yup/Valli validation schemas
    /// </summary>
    ValidationSchemas
}

/// <summary>
/// Формат enum'ов для генерации TypeScript
/// </summary>
public enum TypeScriptEnumFormat
{
    /// <summary>
    /// enum MyEnum { ... }
    /// </summary>
    Enum,
    
    /// <summary>
    /// type MyEnum = "value1" | "value2"
    /// </summary>
    UnionType
}
