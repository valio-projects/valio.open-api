using ValIO.Core.IR.Constraints;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Массив
/// </summary>
public sealed class IrArrayType : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrArrayType()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrArrayType(IrArrayType source)
    {
        Constraints = source.Constraints;
        ElementType = source.ElementType;
    }

    /// <summary>
    /// Тип элементов
    /// </summary>
    public required IrType ElementType { get; init; }

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrArrayType(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<IrType>.Default.Equals(ElementType, ((IrArrayType)other).ElementType);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(ElementType);
}
