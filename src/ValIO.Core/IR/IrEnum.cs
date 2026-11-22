namespace ValIO.Core.IR;

/// <summary>
/// IR представление enum
/// </summary>
public sealed class IrEnum : IEquatable<IrEnum>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrEnum()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrEnum(IrEnum source)
    {
        Name = source.Name;
        _originalName = source._originalName;
        Description = source.Description;
        Values = source.Values;
        BaseType = source.BaseType;
        OriginalSchema = source.OriginalSchema;
    }

    /// <summary>
    /// Имя enum (sanitized, с учётом affix-преобразования, если оно применялось)
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Preserves the explicit original name before applying a fallback.</summary>
    private readonly string? _originalName;

    /// <summary>
    /// Исходное (до affix) имя enum. Используется для имён файлов и путей импорта,
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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrEnum? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<string?>.Default.Equals(_originalName, other._originalName)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<IReadOnlyList<IrEnumValue>>.Default.Equals(Values, other.Values)
        && EqualityComparer<IrEnumBaseType>.Default.Equals(BaseType, other.BaseType)
        && EqualityComparer<IOpenApiSchema?>.Default.Equals(OriginalSchema, other.OriginalSchema);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrEnum other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, _originalName, Description, Values, BaseType, OriginalSchema);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrEnum? left, IrEnum? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrEnum? left, IrEnum? right) => !Equals(left, right);
}

/// <summary>
/// Значение enum
/// </summary>
public sealed class IrEnumValue : IEquatable<IrEnumValue>
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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrEnumValue? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<object>.Default.Equals(Value, other.Value)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrEnumValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Value, Description);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrEnumValue? left, IrEnumValue? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrEnumValue? left, IrEnumValue? right) => !Equals(left, right);
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
