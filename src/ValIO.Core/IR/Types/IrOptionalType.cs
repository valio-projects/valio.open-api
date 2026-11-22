using ValIO.Core.IR.Constraints;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Optional обертка (для TypeScript)
/// </summary>
public sealed class IrOptionalType : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrOptionalType()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrOptionalType(IrOptionalType source)
    {
        Constraints = source.Constraints;
        InnerType = source.InnerType;
    }

    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrOptionalType(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<IrType>.Default.Equals(InnerType, ((IrOptionalType)other).InnerType);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(InnerType);
}
