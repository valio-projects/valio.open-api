namespace ValIO.Core.IR;

/// <summary>
/// IR представление модели (schema)
/// </summary>
public sealed class IrModel : IEquatable<IrModel>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrModel()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrModel(IrModel source)
    {
        Name = source.Name;
        _originalName = source._originalName;
        Description = source.Description;
        Properties = source.Properties;
        Composition = source.Composition;
        ScalarType = source.ScalarType;
        OriginalSchema = source.OriginalSchema;
    }

    /// <summary>
    /// Имя модели (sanitized, с учётом affix-преобразования, если оно применялось)
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Preserves the explicit original name before applying a fallback.</summary>
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

    /// <summary>Gets the value schema of a named scalar component.</summary>
    /// <value>The scalar type with its format, nullability, and constraints, or null for object and composed models.</value>
    public Types.IrType? ScalarType { get; init; }
    
    /// <summary>
    /// Оригинальная OpenAPI схема (для отладки)
    /// </summary>
    public IOpenApiSchema? OriginalSchema { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrModel? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<string?>.Default.Equals(_originalName, other._originalName)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<IReadOnlyList<IrProperty>>.Default.Equals(Properties, other.Properties)
        && EqualityComparer<IrComposition?>.Default.Equals(Composition, other.Composition)
        && EqualityComparer<Types.IrType?>.Default.Equals(ScalarType, other.ScalarType)
        && EqualityComparer<IOpenApiSchema?>.Default.Equals(OriginalSchema, other.OriginalSchema);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrModel other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, _originalName, Description, Properties, Composition, ScalarType, OriginalSchema);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrModel? left, IrModel? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrModel? left, IrModel? right) => !Equals(left, right);
}
