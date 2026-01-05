namespace ValIO.Core.IR;

/// <summary>
/// IR представление enum
/// </summary>
public sealed record IrEnum
{
    /// <summary>
    /// Имя enum (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Значения enum
    /// </summary>
    public required IReadOnlyList<IrEnumValue> Values { get; init; }
    
    /// <summary>
    /// Базовый тип enum (string, integer)
    /// </summary>
    public required IrEnumBaseType BaseType { get; init; }
    
    /// <summary>
    /// Оригинальная OpenAPI схема (для отладки)
    /// </summary>
    public IOpenApiSchema? OriginalSchema { get; init; }
}

/// <summary>
/// Значение enum
/// </summary>
public sealed record IrEnumValue
{
    /// <summary>
    /// Имя значения (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Значение (строка или число)
    /// </summary>
    public required object Value { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Базовый тип enum
/// </summary>
public enum IrEnumBaseType
{
    /// <summary>
    /// Строковый enum
    /// </summary>
    String,
    
    /// <summary>
    /// Числовой enum
    /// </summary>
    Integer
}
