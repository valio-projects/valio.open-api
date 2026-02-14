namespace ValIO.Core.Ast.Enums;

/// <summary>
/// Унарный оператор
/// </summary>
public enum UnaryOperator
{
    Dereference,  // *
    AddressOf,    // &
    LogicalNot,   // !
    Negate,       // -
}
