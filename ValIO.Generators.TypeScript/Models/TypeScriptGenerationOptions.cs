using ValIO.Core.Models;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Опции генерации TypeScript кода
/// </summary>
public class TypeScriptGenerationOptions
{
    /// <summary>
    /// Формат моделей: Interfaces или ValidationSchemas
    /// </summary>
    public ModelFormat ModelFormat { get; set; } = ModelFormat.Interfaces;
    
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
    public EnumFormat EnumFormat { get; set; } = EnumFormat.Enum;
    
    /// <summary>
    /// Использовать strict null checks
    /// </summary>
    public bool UseStrictNullChecks { get; set; } = true;
}

/// <summary>
/// Формат моделей для генерации
/// </summary>
public enum ModelFormat
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
/// Формат enum'ов для генерации
/// </summary>
public enum EnumFormat
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
