namespace ValIO.Generators.FSharp.Ast.Builders;

/// <summary>
/// F# reserved keywords that cannot be used directly as identifiers. Colliding
/// names are wrapped in F#'s double-backtick escape (e.g. <c>``type``</c>).
/// </summary>
internal static class FSharpReservedNames
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        // Common F# keywords.
        "abstract", "and", "as", "assert", "base", "begin", "class", "default",
        "delegate", "do", "done", "downcast", "downto", "elif", "else", "end",
        "exception", "extern", "false", "finally", "fixed", "for", "fun",
        "function", "global", "if", "in", "inherit", "inline", "interface",
        "internal", "lazy", "let", "match", "member", "module", "mutable",
        "namespace", "new", "null", "of", "open", "or", "override", "private",
        "public", "rec", "return", "select", "static", "struct", "then", "to",
        "true", "try", "type", "upcast", "use", "val", "void", "when", "while",
        "with", "yield",
    };

    /// <summary>Wraps a name in <c>``</c> when it collides with an F# keyword.</summary>
    public static string Avoid(string name) =>
        Keywords.Contains(name) ? $"``{name}``" : name;
}
