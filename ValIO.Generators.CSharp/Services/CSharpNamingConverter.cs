using ValIO.Core.Interfaces;
using ValIO.Core.Services;

namespace ValIO.Generators.CSharp.Services;

/// <summary>
/// Реализация конвертера имен для C#
/// </summary>
public class CSharpNamingConverter : NamingConverter
{
    // C# зарезервированные ключевые слова
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };
    
    public override bool IsReservedKeyword(string name)
    {
        return ReservedKeywords.Contains(name);
    }
    
    public override string SanitizeReservedKeyword(string name)
    {
        if (IsReservedKeyword(name))
        {
            return $"@{name}";
        }
        return name;
    }
}
