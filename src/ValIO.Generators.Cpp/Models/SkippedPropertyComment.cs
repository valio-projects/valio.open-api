using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Информация о пропущенном свойстве (например, рекурсивном) для вывода в комментарий.
/// </summary>
public sealed class SkippedPropertyComment : IEquatable<SkippedPropertyComment>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="PropertyName">The source property name.</param>
    /// <param name="Type">The property type available for diagnostics.</param>
    /// <param name="Reason">The explanation written in generated code.</param>
    public SkippedPropertyComment(string PropertyName, TypeNode Type, string Reason)
    {
        this.PropertyName = PropertyName;
        this.Type = Type;
        this.Reason = Reason;
    }

    /// <summary>Gets the source property name.</summary>
    /// <value>The source property name.</value>
    public string PropertyName { get; init; }

    /// <summary>Gets the property type available for diagnostics.</summary>
    /// <value>The property type available for diagnostics.</value>
    public TypeNode Type { get; init; }

    /// <summary>Gets the explanation written in generated code.</summary>
    /// <value>The explanation written in generated code.</value>
    public string Reason { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(SkippedPropertyComment? other) => other is not null
        && EqualityComparer<string>.Default.Equals(PropertyName, other.PropertyName)
        && EqualityComparer<TypeNode>.Default.Equals(Type, other.Type)
        && EqualityComparer<string>.Default.Equals(Reason, other.Reason);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SkippedPropertyComment other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PropertyName, Type, Reason);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(SkippedPropertyComment? left, SkippedPropertyComment? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(SkippedPropertyComment? left, SkippedPropertyComment? right) => !Equals(left, right);
}
