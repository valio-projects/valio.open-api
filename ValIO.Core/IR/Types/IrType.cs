using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// IR представление типа (language-agnostic)
/// </summary>
public abstract record IrType
{
    /// <summary>
    /// Тип узла
    /// </summary>
    public abstract IrTypeKind Kind { get; }
}
