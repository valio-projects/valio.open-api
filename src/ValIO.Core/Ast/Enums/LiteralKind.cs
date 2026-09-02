namespace ValIO.Core.Ast.Enums;

/// <summary>
/// Тип литерала
/// </summary>
public enum LiteralKind
{
    String,
    Number,
    Boolean,
    Null,
    Character,
    /// <summary>
    /// Кавыченный строковый литерал в C-стиле: рендер обязан сам добавить кавычки и
    /// экранирование (напр. "wire-key" в j["wire-key"]). В отличие от String
    /// (платформенные обёртки вроде UE TEXT()).
    /// </summary>
    QuotedString
}
