namespace ValIO.Core.IR.Types;

/// <summary>
/// Nullable обертка
/// </summary>
public sealed record IrNullableType : IrType
{
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }
}
