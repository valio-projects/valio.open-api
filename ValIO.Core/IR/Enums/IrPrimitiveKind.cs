namespace ValIO.Core.IR.Enums;

/// <summary>
/// Примитивный тип в IR
/// </summary>
public enum IrPrimitiveKind
{
    /// <summary>
    /// Строка
    /// </summary>
    String,
    
    /// <summary>
    /// Число (float/double)
    /// </summary>
    Number,
    
    /// <summary>
    /// Целое число (int/long)
    /// </summary>
    Integer,
    
    /// <summary>
    /// Логическое значение
    /// </summary>
    Boolean,
    
    /// <summary>
    /// Дата и время
    /// </summary>
    DateTime,
    
    /// <summary>
    /// Дата
    /// </summary>
    Date,
    
    /// <summary>
    /// Время
    /// </summary>
    Time,
    
    /// <summary>
    /// Бинарные данные (файл)
    /// </summary>
    Binary,
    
    /// <summary>
    /// Байтовый массив
    /// </summary>
    ByteArray,
    
    /// <summary>
    /// UUID/GUID
    /// </summary>
    Uuid
}
