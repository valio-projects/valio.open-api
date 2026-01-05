using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// IR представление свойства модели
/// </summary>
public sealed record IrProperty
{
    /// <summary>
    /// Имя свойства (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип свойства
    /// </summary>
    public required IrType Type { get; init; }
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public required bool IsRequired { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Ограничения (constraints)
    /// </summary>
    public IrPropertyConstraints? Constraints { get; init; }
}
