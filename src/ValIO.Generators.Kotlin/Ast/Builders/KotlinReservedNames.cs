namespace ValIO.Generators.Kotlin.Ast.Builders;

/// <summary>
/// Kotlin keywords/soft keywords that cannot be used directly as identifiers.
/// </summary>
internal static class KotlinReservedNames
{
    private static readonly HashSet<string> HardKeywords = new(StringComparer.Ordinal)
    {
        "as", "break", "class", "continue", "do", "else", "false", "for", "fun",
        "if", "in", "interface", "is", "null", "object", "package", "return",
        "super", "this", "throw", "true", "try", "typealias", "typeof", "val",
        "var", "when", "while",
    };

    /// <summary>Wraps a name in backticks when it collides with a Kotlin hard keyword.</summary>
    public static string Avoid(string name) =>
        HardKeywords.Contains(name) ? "`" + name + "`" : name;
}
