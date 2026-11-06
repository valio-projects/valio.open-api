using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// IR представление свойства модели
/// </summary>
public sealed record IrProperty
{
    /// <summary>
    /// Оригинальное имя свойства из OpenAPI схемы (до обработки)
    /// </summary>
    public required string OriginalName { get; init; }
    
    /// <summary>
    /// Имя свойства (sanitized, безопасное для использования в коде, обработано через NamingConverter)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип свойства
    /// </summary>
    public required IrType Type { get; init; }
    
    /// <summary>
    /// Gets whether the containing object requires this property to be present.
    /// </summary>
    /// <remarks>Presence is independent of whether <see cref="Type"/> permits JSON null.</remarks>
    /// <value>Whether omitting the property violates the source contract.</value>
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
