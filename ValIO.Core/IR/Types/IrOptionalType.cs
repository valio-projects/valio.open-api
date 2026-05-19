namespace ValIO.Core.IR.Types;

/// <summary>
/// Optional обертка (для TypeScript)
/// </summary>
public sealed record IrOptionalType : IrType
{
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }
}
