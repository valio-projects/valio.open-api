namespace ValIO.Generators.Swift.Ast.Builders;

/// <summary>
/// Swift keywords that cannot be used directly as identifiers.
/// Names that collide are wrapped in backticks (Swift's identifier-escape mechanism).
/// </summary>
internal static class SwiftReservedNames
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        // Declarations
        "associatedtype", "class", "deinit", "enum", "extension", "fileprivate",
        "func", "import", "init", "inout", "internal", "let", "open", "operator",
        "private", "precedencegroup", "protocol", "public", "rethrows", "static",
        "struct", "subscript", "typealias", "var",
        // Statements
        "break", "case", "catch", "continue", "default", "defer", "do", "else",
        "fallthrough", "for", "guard", "if", "in", "repeat", "return", "throw",
        "switch", "where", "while",
        // Expressions and types
        "Any", "as", "false", "is", "nil", "self", "Self", "super", "throws",
        "true", "try",
    };

    /// <summary>Wraps a name in backticks when it collides with a Swift keyword.</summary>
    public static string Avoid(string name) =>
        Keywords.Contains(name) ? "`" + name + "`" : name;
}
