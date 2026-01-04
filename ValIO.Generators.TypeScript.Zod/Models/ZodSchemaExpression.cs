namespace ValIO.Generators.TypeScript.Zod.Models;

/// <summary>
/// Абстрактное представление Zod схемы (AST для Zod)
/// Используется для разделения ответственности: маппер создает структуру, рендерер генерирует строки
/// </summary>
public abstract class ZodSchemaExpression
{
    /// <summary>
    /// Валидации, применяемые к схеме (min, max, pattern и т.д.)
    /// </summary>
    public ZodValidationConstraints? Validations { get; set; }
}

/// <summary>
/// Ссылка на другую схему (например, UserSchema)
/// </summary>
public class ZodReferenceExpression : ZodSchemaExpression
{
    /// <summary>
    /// Имя схемы (например, "UserSchema")
    /// </summary>
    public string SchemaName { get; set; } = string.Empty;
    
    /// <summary>
    /// Является ли это enum схемой
    /// </summary>
    public bool IsEnum { get; set; }
}

/// <summary>
/// Циклическая ссылка (z.lazy)
/// </summary>
public class ZodLazyExpression : ZodSchemaExpression
{
    /// <summary>
    /// Имя схемы для lazy загрузки
    /// </summary>
    public string SchemaName { get; set; } = string.Empty;
}

/// <summary>
/// Примитивный тип (z.boolean(), z.number(), z.string() и т.д.)
/// </summary>
public class ZodPrimitiveExpression : ZodSchemaExpression
{
    /// <summary>
    /// Тип примитива
    /// </summary>
    public ZodPrimitiveType Type { get; set; }
    
    /// <summary>
    /// Формат (для строк: date, date-time, uuid, uri, email, binary)
    /// </summary>
    public string? Format { get; set; }
}

/// <summary>
/// Объект (z.object({...}))
/// </summary>
public class ZodObjectExpression : ZodSchemaExpression
{
    /// <summary>
    /// Свойства объекта
    /// </summary>
    public Dictionary<string, ZodPropertyExpression> Properties { get; set; } = new();
}

/// <summary>
/// Свойство объекта
/// </summary>
public class ZodPropertyExpression
{
    /// <summary>
    /// Выражение схемы для свойства
    /// </summary>
    public ZodSchemaExpression Expression { get; set; } = null!;
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public bool IsRequired { get; set; }
}

/// <summary>
/// Массив (z.array(...))
/// </summary>
public class ZodArrayExpression : ZodSchemaExpression
{
    /// <summary>
    /// Выражение схемы для элементов массива
    /// </summary>
    public ZodSchemaExpression ItemExpression { get; set; } = null!;
}

/// <summary>
/// Union тип (z.union([...]))
/// </summary>
public class ZodUnionExpression : ZodSchemaExpression
{
    /// <summary>
    /// Список выражений для union
    /// </summary>
    public List<ZodSchemaExpression> Expressions { get; set; } = new();
}

/// <summary>
/// Intersection тип (для allOf - объединение свойств объектов)
/// </summary>
public class ZodIntersectionExpression : ZodSchemaExpression
{
    /// <summary>
    /// Список выражений для intersection
    /// </summary>
    public List<ZodSchemaExpression> Expressions { get; set; } = new();
}

/// <summary>
/// Record/словарь (z.record(z.string(), ...))
/// </summary>
public class ZodRecordExpression : ZodSchemaExpression
{
    /// <summary>
    /// Выражение схемы для значений словаря
    /// </summary>
    public ZodSchemaExpression ValueExpression { get; set; } = null!;
}

/// <summary>
/// Enum схема (z.enum([...]) или z.union([z.literal(...), ...]))
/// </summary>
public class ZodEnumExpression : ZodSchemaExpression
{
    /// <summary>
    /// Имя enum схемы (если это ссылка)
    /// </summary>
    public string? SchemaName { get; set; }
    
    /// <summary>
    /// Значения enum (если это inline enum)
    /// </summary>
    public List<ZodEnumValue>? Values { get; set; }
    
    /// <summary>
    /// Базовый тип enum (string, number)
    /// </summary>
    public string UnderlyingType { get; set; } = "string";
}

/// <summary>
/// Значение enum
/// </summary>
public class ZodEnumValue
{
    /// <summary>
    /// Строковое представление значения (для рендеринга)
    /// </summary>
    public string ValueString { get; set; } = string.Empty;
}

/// <summary>
/// Типы примитивов Zod
/// </summary>
public enum ZodPrimitiveType
{
    Any,
    Boolean,
    Number,
    String,
    InstanceOf
}

/// <summary>
/// Ограничения валидации для Zod схем
/// </summary>
public class ZodValidationConstraints
{
    /// <summary>
    /// Минимальное значение (для number)
    /// </summary>
    public string? Minimum { get; set; }
    
    /// <summary>
    /// Максимальное значение (для number)
    /// </summary>
    public string? Maximum { get; set; }
    
    /// <summary>
    /// Минимальная длина (для string, array)
    /// </summary>
    public int? MinLength { get; set; }
    
    /// <summary>
    /// Максимальная длина (для string, array)
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Регулярное выражение (для string)
    /// </summary>
    public string? Pattern { get; set; }
}
