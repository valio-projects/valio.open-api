using ValIO.Core.Interfaces;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Models.TemplateData;

/// <summary>
/// Универсальные данные для шаблона enum (enum, union type, schema)
/// </summary>
/// <typeparam name="TOptions">Тип опций (язык-специфичный)</typeparam>
public class EnumTemplateData<TOptions> : IEnumTemplateData where TOptions : EnumTemplateDataOptions, new()
{
    /// <summary>
    /// Имя enum
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание enum
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Значения enum
    /// </summary>
    public IReadOnlyList<EnumValueTemplateData> Values { get; set; } = Array.Empty<EnumValueTemplateData>();
    
    /// <summary>
    /// Язык генерации
    /// </summary>
    public GenerationLanguage Language { get; set; }
    
    /// <summary>
    /// Формат enum (enum, union, schema)
    /// </summary>
    public EnumFormat Format { get; set; } = EnumFormat.Enum;
    
    /// <summary>
    /// Базовый тип для enum (для C#: int, long, byte и т.д.)
    /// </summary>
    public string? UnderlyingType { get; set; }
    
    /// <summary>
    /// Namespace (для C#)
    /// </summary>
    public string? Namespace { get; set; }
    
    /// <summary>
    /// Путь к файлу для импорта (для TypeScript)
    /// </summary>
    public string? ImportPath { get; set; }
    
    /// <summary>
    /// Дополнительные опции для генерации (язык-специфичные)
    /// </summary>
    public TOptions Options { get; set; } = new();
}

/// <summary>
/// Универсальные данные для шаблона enum (enum, union type, schema)
/// Безтипная версия для обратной совместимости
/// </summary>
public class EnumTemplateData : EnumTemplateData<EnumTemplateDataOptions>
{
}

/// <summary>
/// Универсальные данные для шаблона значения enum
/// </summary>
public class EnumValueTemplateData
{
    /// <summary>
    /// Имя значения enum
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Значение enum
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// Описание значения
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Строковое представление значения (для генерации кода)
    /// </summary>
    public string ValueString { get; set; } = string.Empty;
}

/// <summary>
/// Формат enum для генерации
/// </summary>
public enum EnumFormat
{
    /// <summary>
    /// Enum (C# enum, TypeScript enum)
    /// </summary>
    Enum,
    
    /// <summary>
    /// Union type (TypeScript: "A" | "B" | "C")
    /// </summary>
    Union,
    
    /// <summary>
    /// Схема валидации (Zod enum schema)
    /// </summary>
    ValidationSchema
}
