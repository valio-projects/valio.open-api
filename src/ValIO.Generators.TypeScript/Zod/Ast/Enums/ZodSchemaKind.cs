namespace ValIO.Generators.TypeScript.Zod.Ast.Enums;

/// <summary>
/// Тип Zod схемы
/// </summary>
public enum ZodSchemaKind
{
    Object,
    Array,
    Union,
    Intersection,
    Enum,
    String,
    Number,
    Integer,
    BigInt,
    Boolean,
    Null,
    Any,
    /// <summary>
    /// Схема в виде выражения (цепочка вызовов z.string().uuid().min() и т.д.)
    /// </summary>
    Expression,
    Optional,
    Nullable,
    Effects,
    InstanceOf
}
