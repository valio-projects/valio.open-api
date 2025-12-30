using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Данные для шаблона интерфейса TypeScript
/// </summary>
public class TypeScriptModelTemplateData
{
    public string InterfaceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<TypeScriptPropertyTemplateData> Properties { get; set; } = Array.Empty<TypeScriptPropertyTemplateData>();
    
    /// <summary>
    /// Использовать strict null checks
    /// </summary>
    public bool UseStrictNullChecks { get; set; } = true;
}

/// <summary>
/// Данные для шаблона свойства TypeScript
/// </summary>
public class TypeScriptPropertyTemplateData
{
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе свойства с мета-данными
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Строковое представление типа (для обратной совместимости)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Является ли свойство опциональным (добавляется "?")
    /// </summary>
    public bool IsOptional { get; set; }
    
    public string? Description { get; set; }
}

/// <summary>
/// Данные для шаблона enum TypeScript
/// </summary>
public class TypeScriptEnumTemplateData
{
    public string EnumName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EnumFormat EnumFormat { get; set; } = EnumFormat.Enum;
    public IReadOnlyList<TypeScriptEnumValueTemplateData> Values { get; set; } = Array.Empty<TypeScriptEnumValueTemplateData>();
}

/// <summary>
/// Данные для шаблона значения enum TypeScript
/// </summary>
public class TypeScriptEnumValueTemplateData
{
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string? Description { get; set; }
    public string ValueString { get; set; } = string.Empty;
}

