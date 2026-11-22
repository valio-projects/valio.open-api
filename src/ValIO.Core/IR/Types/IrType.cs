using ValIO.Core.IR.Constraints;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Represents a language-independent value type and its local schema constraints.
/// </summary>
public abstract class IrType : IEquatable<IrType>
{
    /// <summary>Gets constraints declared on this value schema, including individual composition arms.</summary>
    /// <value>Local validation metadata, or no constraints when the schema declares none.</value>
    public IrPropertyConstraints? Constraints { get; init; }

    /// <summary>Copies the type with the supplied local constraints.</summary>
    /// <param name="constraints">The complete local constraint metadata to apply.</param>
    /// <returns>A value of the same concrete type with the specified constraints.</returns>
    public abstract IrType WithConstraints(IrPropertyConstraints? constraints);

    /// <summary>Determines whether the type's representation and local metadata equal another's.</summary>
    /// <param name="other">The type to compare.</param>
    /// <returns>True when both have the same concrete type, stored value, and constraints.</returns>
    public bool Equals(IrType? other) => ReferenceEquals(this, other) ||
        other is not null && GetType() == other.GetType() && Equals(Constraints, other.Constraints) && EqualsType(other);

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) => obj is IrType other && Equals(other);

    /// <inheritdoc />
    public sealed override int GetHashCode() => HashCode.Combine(GetType(), Constraints, GetTypeHashCode());

    /// <summary>Compares the members stored by this concrete type.</summary>
    /// <param name="other">An instance with the same runtime type.</param>
    /// <returns>True when both concrete values are equal.</returns>
    protected abstract bool EqualsType(IrType other);

    /// <summary>Hashes the members stored by this concrete type.</summary>
    /// <returns>A hash of the concrete value.</returns>
    protected abstract int GetTypeHashCode();

    /// <summary>Compares two type values for equality.</summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns>True when both are null or carry the same type data.</returns>
    public static bool operator ==(IrType? left, IrType? right) => Equals(left, right);

    /// <summary>Compares two type values for inequality.</summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns>True when the types differ.</returns>
    public static bool operator !=(IrType? left, IrType? right) => !Equals(left, right);
}
