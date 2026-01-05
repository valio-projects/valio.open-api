namespace ValIO.Core.IR.Enums;

/// <summary>
/// Тип IR узла типа
/// </summary>
public enum IrTypeKind
{
    /// <summary>
    /// Примитивный тип (string, number, integer, boolean)
    /// </summary>
    Primitive,
    
    /// <summary>
    /// Ссылка на модель
    /// </summary>
    ModelReference,
    
    /// <summary>
    /// Массив
    /// </summary>
    Array,
    
    /// <summary>
    /// Словарь
    /// </summary>
    Dictionary,
    
    /// <summary>
    /// Union (oneOf, anyOf)
    /// </summary>
    Union,
    
    /// <summary>
    /// Intersection (allOf)
    /// </summary>
    Intersection,
    
    /// <summary>
    /// Nullable обертка
    /// </summary>
    Nullable,
    
    /// <summary>
    /// Optional обертка (для TypeScript)
    /// </summary>
    Optional
}
