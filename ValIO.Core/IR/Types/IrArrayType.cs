using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Массив
/// </summary>
public sealed record IrArrayType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Array;
    
    /// <summary>
    /// Тип элементов
    /// </summary>
    public required IrType ElementType { get; init; }
}
