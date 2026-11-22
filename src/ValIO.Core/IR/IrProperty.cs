using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// IR представление свойства модели
/// </summary>
public sealed class IrProperty : IEquatable<IrProperty>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrProperty()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrProperty(IrProperty source)
    {
        OriginalName = source.OriginalName;
        Name = source.Name;
        Type = source.Type;
        IsRequired = source.IsRequired;
        Description = source.Description;
        Constraints = source.Constraints;
    }

    /// <summary>
    /// Оригинальное имя свойства из OpenAPI схемы (до обработки)
    /// </summary>
    public required string OriginalName { get; init; }
    
    /// <summary>
    /// Имя свойства (sanitized, безопасное для использования в коде, обработано через NamingConverter)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип свойства
    /// </summary>
    public required IrType Type { get; init; }
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public required bool IsRequired { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Ограничения (constraints)
    /// </summary>
    public IrPropertyConstraints? Constraints { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrProperty? other) => other is not null
        && EqualityComparer<string>.Default.Equals(OriginalName, other.OriginalName)
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<IrType>.Default.Equals(Type, other.Type)
        && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<IrPropertyConstraints?>.Default.Equals(Constraints, other.Constraints);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrProperty other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(OriginalName, Name, Type, IsRequired, Description, Constraints);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrProperty? left, IrProperty? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrProperty? left, IrProperty? right) => !Equals(left, right);
}
