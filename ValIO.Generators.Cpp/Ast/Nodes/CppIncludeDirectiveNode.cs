using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел директивы #include для C++ заголовочных файлов.
/// Поддерживает как системные (#include &lt;...&gt;), так и пользовательские (#include "...") включения.
/// </summary>
public sealed class CppIncludeDirectiveNode : AstNode
{
    /// <summary>
    /// Путь к включаемому файлу (без кавычек/скобок)
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Является ли включение системным (angle brackets vs quotes).
    /// true = #include &lt;path&gt;, false = #include "path"
    /// </summary>
    public bool IsSystemInclude { get; }


    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppIncludeDirectiveNode(
        string path,
        bool isSystemInclude = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        IsSystemInclude = isSystemInclude;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppIncludeDirective(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppIncludeDirectiveNode");
    }

    public override AstNode Clone()
    {
        return new CppIncludeDirectiveNode(Path, IsSystemInclude, Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var brackets = IsSystemInclude ? $"<{Path}>" : $"\"{Path}\"";
        return $"#include {brackets}";
    }
}
