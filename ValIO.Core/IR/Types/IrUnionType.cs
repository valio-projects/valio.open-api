using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Union (oneOf, anyOf)
/// </summary>
public sealed record IrUnionType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Union;
    
    /// <summary>
    /// Типы в union
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
}
