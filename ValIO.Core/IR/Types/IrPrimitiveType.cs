using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Примитивный тип
/// </summary>
public sealed record IrPrimitiveType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Primitive;
    
    /// <summary>
    /// Примитивный тип (string, number, integer, boolean)
    /// </summary>
    public required IrPrimitiveKind PrimitiveKind { get; init; }
}
