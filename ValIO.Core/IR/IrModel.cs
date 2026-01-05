namespace ValIO.Core.IR;

/// <summary>
/// IR представление модели (schema)
/// </summary>
public sealed record IrModel
{
    /// <summary>
    /// Имя модели (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Свойства модели
    /// </summary>
    public required IReadOnlyList<IrProperty> Properties { get; init; }
    
    /// <summary>
    /// Композиция (allOf, oneOf, anyOf)
    /// </summary>
    public IrComposition? Composition { get; init; }
    
    /// <summary>
    /// Оригинальная OpenAPI схема (для отладки)
    /// </summary>
    public OpenApiSchema? OriginalSchema { get; init; }
}
