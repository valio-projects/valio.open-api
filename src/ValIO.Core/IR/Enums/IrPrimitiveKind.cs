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
    /// Целое число (int32)
    /// </summary>
    Integer,
    
    /// <summary>
    /// Длинное целое (int64) — в TypeScript/JS маппится в bigint
    /// </summary>
    Long,
    
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
    Uuid,
    
    /// <summary>
    /// Любой тип (any/object)
    /// </summary>
    Any,
    
    /// <summary>
    /// Пустой тип (void)
    /// </summary>
    Void,

    /// <summary>
    /// JSON null литерал (OpenAPI 3.1 <c>{"type":"null"}</c>) — значение всегда null.
    /// В отличие от Any/Void это строгий, валидируемый тип (например <c>z.null()</c>).
    /// </summary>
    Null,

    /// <summary>
    /// Файл
    /// </summary>
    File
}
