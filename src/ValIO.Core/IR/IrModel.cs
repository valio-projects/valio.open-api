namespace ValIO.Core.IR;

/// <summary>
/// IR представление модели (schema)
/// </summary>
public sealed record IrModel
{
    /// <summary>
    /// Имя модели (sanitized, с учётом affix-преобразования, если оно применялось)
    /// </summary>
    public required string Name { get; init; }

    private readonly string? _originalName;

    /// <summary>
    /// Исходное (до affix) имя модели. Используется для имён файлов и путей импорта,
    /// которые не должны меняться при добавлении prefix/suffix. По умолчанию равно
    /// <see cref="Name"/>, если affix не применялся.
    /// </summary>
    public string OriginalName
    {
        get => _originalName ?? Name;
        init => _originalName = value;
    }

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
    public IOpenApiSchema? OriginalSchema { get; init; }
}
