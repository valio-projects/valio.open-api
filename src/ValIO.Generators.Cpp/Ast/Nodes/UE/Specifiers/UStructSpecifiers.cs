using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса USTRUCT(...).
/// Определяет поведение структуры в Unreal Engine Reflection System.
/// </summary>
public sealed class UStructSpecifiers : IEquatable<UStructSpecifiers>
{
    /// <summary>
    /// Включить BlueprintType — позволяет использовать структуру в Blueprints.
    /// USTRUCT(BlueprintType)
    /// </summary>
    public bool BlueprintType { get; init; } = true;

    /// <summary>
    /// Category для группировки в Blueprint Editor и Details Panel.
    /// USTRUCT(BlueprintType, Category = "ValIO|Models")
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Meta-спецификаторы: meta = (Key = "Value", ...).
    /// Пример: meta = (ShortToolTip = "Result of Get Queue Tasks List")
    /// </summary>
    public IReadOnlyDictionary<string, string> Meta { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(UStructSpecifiers? other) => other is not null
        && EqualityComparer<bool>.Default.Equals(BlueprintType, other.BlueprintType)
        && EqualityComparer<string?>.Default.Equals(Category, other.Category)
        && EqualityComparer<IReadOnlyDictionary<string, string>>.Default.Equals(Meta, other.Meta);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UStructSpecifiers other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(BlueprintType, Category, Meta);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(UStructSpecifiers? left, UStructSpecifiers? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(UStructSpecifiers? left, UStructSpecifiers? right) => !Equals(left, right);
}
