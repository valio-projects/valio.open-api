using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Nullable обертка
/// </summary>
public sealed record IrNullableType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Nullable;
    
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }
}
