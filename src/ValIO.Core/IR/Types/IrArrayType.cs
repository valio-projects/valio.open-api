namespace ValIO.Core.IR.Types;

/// <summary>
/// Массив
/// </summary>
public sealed record IrArrayType : IrType
{
    /// <summary>
    /// Тип элементов
    /// </summary>
    public required IrType ElementType { get; init; }
}
