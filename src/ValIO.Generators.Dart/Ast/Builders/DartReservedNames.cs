namespace ValIO.Generators.Dart.Ast.Builders;

/// <summary>
/// Dart-language reserved words and built-in identifiers that cannot be
/// used as plain identifiers. Tiny static list — covers the words that
/// actually appear as schema property names in practice.
/// </summary>
internal static class DartReservedNames
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "abstract", "as", "assert", "async", "await", "break", "case", "catch",
        "class", "const", "continue", "covariant", "default", "deferred", "do",
        "dynamic", "else", "enum", "export", "extends", "extension", "external",
        "factory", "false", "final", "finally", "for", "function", "get", "hide",
        "if", "implements", "import", "in", "interface", "is", "late", "library",
        "mixin", "new", "null", "of", "on", "operator", "part", "rethrow", "return",
        "set", "show", "static", "super", "switch", "sync", "this", "throw", "true",
        "try", "typedef", "var", "void", "while", "with", "yield",
    };

    public static string Avoid(string name) =>
        Reserved.Contains(name) ? name + "_" : name;
}
