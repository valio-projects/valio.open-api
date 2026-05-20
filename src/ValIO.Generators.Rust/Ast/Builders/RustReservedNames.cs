namespace ValIO.Generators.Rust.Ast.Builders;

/// <summary>
/// Rust keywords that cannot be used directly as identifiers.
/// Colliding names are prefixed with <c>r#</c> (Rust's raw-identifier syntax).
/// </summary>
internal static class RustReservedNames
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "break", "const", "continue", "crate", "else", "enum", "extern",
        "false", "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod",
        "move", "mut", "pub", "ref", "return", "self", "Self", "static", "struct",
        "super", "trait", "true", "type", "unsafe", "use", "where", "while", "async",
        "await", "dyn",
        // Reserved for future use.
        "abstract", "become", "box", "do", "final", "macro", "override", "priv",
        "typeof", "unsized", "virtual", "yield", "try",
    };

    /// <summary>Returns <c>r#name</c> when the name collides with a Rust keyword.</summary>
    public static string Avoid(string name) =>
        Keywords.Contains(name) ? "r#" + name : name;
}
