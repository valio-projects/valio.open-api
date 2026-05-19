namespace ValIO.Core.IR.Types;

/// <summary>
/// Словарь
/// </summary>
public sealed record IrDictionaryType : IrType
{
    /// <summary>
    /// Тип ключа
    /// </summary>
    public required IrType KeyType { get; init; }

    /// <summary>
    /// Тип значения
    /// </summary>
    public required IrType ValueType { get; init; }
}
