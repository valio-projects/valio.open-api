using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Represents schema alternatives with their original matching cardinality.
/// </summary>
public sealed class IrUnionType : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrUnionType()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrUnionType(IrUnionType source)
    {
        Constraints = source.Constraints;
        Kind = source.Kind;
        Types = source.Types;
    }

    /// <summary>Gets the matching rule for the alternatives.</summary>
    /// <value>Inclusive anyOf by default, or exclusive oneOf when declared by the schema.</value>
    public IrUnionKind Kind { get; init; } = IrUnionKind.AnyOf;

    /// <summary>
    /// Типы в union
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrUnionType(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<IrUnionKind>.Default.Equals(Kind, ((IrUnionType)other).Kind)
        && EqualityComparer<IReadOnlyList<IrType>>.Default.Equals(Types, ((IrUnionType)other).Types);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(Kind, Types);
}
