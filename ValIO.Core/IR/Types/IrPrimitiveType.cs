using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Примитивный тип
/// </summary>
public sealed record IrPrimitiveType : IrType
{
    /// <summary>
    /// Примитивный тип (string, number, integer, boolean)
    /// </summary>
    public required IrPrimitiveKind PrimitiveKind { get; init; }
}
