using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Python import statement:
///   <c>import foo</c>
///   <c>import foo as bar</c>
///   <c>from foo import bar, baz</c>
///   <c>from foo import bar as qux</c>
/// </summary>
public sealed class PythonImportNode : AstNode
{
    /// <summary>Источник импорта (после "from"). Пустая строка — простой "import X".</summary>
    public string Module { get; }

    /// <summary>Список имён для "from M import name1, name2". Пустой для "import M".</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Алиас для "import M as Alias" / "from M import name as Alias".</summary>
    public string? Alias { get; }

    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    public PythonImportNode(
        string module,
        IReadOnlyList<string>? names = null,
        string? alias = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Names = names?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Alias = alias;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonImport(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonImportNode");
    }

    public override AstNode Clone() =>
        new PythonImportNode(Module, Names.ToImmutableList(), Alias, Metadata.ToImmutableDictionary());
}
