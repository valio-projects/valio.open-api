using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// Тип композиции схемы
/// </summary>
public enum IrCompositionKind
{
    /// <summary>
    /// AllOf (intersection)
    /// </summary>
    AllOf,
    
    /// <summary>
    /// OneOf (discriminated union)
    /// </summary>
    OneOf,
    
    /// <summary>
    /// AnyOf (union)
    /// </summary>
    AnyOf
}

/// <summary>
/// Композиция схемы (allOf, oneOf, anyOf)
/// </summary>
public sealed class IrComposition : IEquatable<IrComposition>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrComposition()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrComposition(IrComposition source)
    {
        Kind = source.Kind;
        Types = source.Types;
        DiscriminatorPropertyName = source.DiscriminatorPropertyName;
        DiscriminatorMapping = source.DiscriminatorMapping;
    }

    /// <summary>
    /// Тип композиции
    /// </summary>
    public required IrCompositionKind Kind { get; init; }
    
    /// <summary>
    /// Типы в композиции
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
    
    /// <summary>
    /// Discriminator для oneOf (если есть)
    /// </summary>
    public string? DiscriminatorPropertyName { get; init; }
    
    /// <summary>
    /// Mapping значений discriminator для oneOf (если есть)
    /// </summary>
    public IReadOnlyDictionary<string, string>? DiscriminatorMapping { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrComposition? other) => other is not null
        && EqualityComparer<IrCompositionKind>.Default.Equals(Kind, other.Kind)
        && EqualityComparer<IReadOnlyList<IrType>>.Default.Equals(Types, other.Types)
        && EqualityComparer<string?>.Default.Equals(DiscriminatorPropertyName, other.DiscriminatorPropertyName)
        && EqualityComparer<IReadOnlyDictionary<string, string>?>.Default.Equals(DiscriminatorMapping, other.DiscriminatorMapping);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrComposition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Types, DiscriminatorPropertyName, DiscriminatorMapping);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrComposition? left, IrComposition? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrComposition? left, IrComposition? right) => !Equals(left, right);
}
