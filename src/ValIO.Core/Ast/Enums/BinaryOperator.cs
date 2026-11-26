namespace ValIO.Core.Ast.Enums;

/// <summary>
/// Бинарный оператор
/// </summary>
public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LogicalAnd,
    LogicalOr,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,
    Assign,
    /// <summary>Nullish coalescing (??)</summary>
    NullishCoalescing,
    /// <summary>Passes a value as the final argument of a function.</summary>
    Pipe,
    /// <summary>Tests whether two operands are distinct object identities.</summary>
    IsNot,
    /// <summary>Tests whether two operands are the same object identity.</summary>
    Is
}
