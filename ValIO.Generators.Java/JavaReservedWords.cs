namespace ValIO.Generators.Java;

/// <summary>
/// Java reserved keywords (Java 21) used to avoid colliding with identifiers in generated code.
/// </summary>
internal static class JavaReservedWords
{
    public static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class",
        "const", "continue", "default", "do", "double", "else", "enum", "extends", "final",
        "finally", "float", "for", "goto", "if", "implements", "import", "instanceof", "int",
        "interface", "long", "native", "new", "non-sealed", "null", "package", "permits",
        "private", "protected", "public", "record", "return", "sealed", "short", "static",
        "strictfp", "super", "switch", "synchronized", "this", "throw", "throws", "transient",
        "true", "false", "try", "var", "void", "volatile", "while", "yield",
    };
}
