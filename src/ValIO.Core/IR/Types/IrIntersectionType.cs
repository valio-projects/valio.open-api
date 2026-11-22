using ValIO.Core.IR.Constraints;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Intersection (allOf)
/// </summary>
public sealed class IrIntersectionType : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrIntersectionType()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrIntersectionType(IrIntersectionType source)
    {
        Constraints = source.Constraints;
        Types = source.Types;
    }

    /// <summary>
    /// Типы в intersection
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrIntersectionType(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<IReadOnlyList<IrType>>.Default.Equals(Types, ((IrIntersectionType)other).Types);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(Types);
}
