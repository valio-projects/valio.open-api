using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Optional обертка (для TypeScript)
/// </summary>
public sealed record IrOptionalType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Optional;
    
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }
}
