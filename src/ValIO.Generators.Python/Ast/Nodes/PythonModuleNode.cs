using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Python модуль (один .py файл).
/// Содержит "future imports", обычные импорты, "if TYPE_CHECKING" импорты и
/// объявления верхнего уровня.
/// </summary>
public sealed class PythonModuleNode : AstNode
{
    /// <summary>Doc-string модуля (опциональный).</summary>
    public CommentNode? ModuleDoc { get; }

    /// <summary>__future__ импорты (например, "from __future__ import annotations").</summary>
    public IReadOnlyList<PythonImportNode> FutureImports { get; }

    /// <summary>Обычные импорты (расположены после future-импортов).</summary>
    public IReadOnlyList<PythonImportNode> Imports { get; }

    /// <summary>Импорты, доступные только во время статического анализа типов.</summary>
    public IReadOnlyList<PythonImportNode> TypeCheckingImports { get; }

    /// <summary>Декларации верхнего уровня (классы, функции, переменные).</summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; }

    /// <summary>Trailing-statements, выполняемые после деклараций (например, model_rebuild()).</summary>
    public IReadOnlyList<string> TrailingStatements { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (ModuleDoc != null) children.Add(ModuleDoc);
            children.AddRange(FutureImports);
            children.AddRange(Imports);
            children.AddRange(TypeCheckingImports);
            children.AddRange(Declarations);
            return children;
        }
    }

    public PythonModuleNode(
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyList<PythonImportNode>? imports = null,
        IReadOnlyList<PythonImportNode>? typeCheckingImports = null,
        IReadOnlyList<PythonImportNode>? futureImports = null,
        CommentNode? moduleDoc = null,
        IReadOnlyList<string>? trailingStatements = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        ModuleDoc = moduleDoc;
        FutureImports = futureImports?.ToImmutableList() ?? ImmutableList<PythonImportNode>.Empty;
        Imports = imports?.ToImmutableList() ?? ImmutableList<PythonImportNode>.Empty;
        TypeCheckingImports = typeCheckingImports?.ToImmutableList() ?? ImmutableList<PythonImportNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        TrailingStatements = trailingStatements?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonModule(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonModuleNode");
    }

    public override AstNode Clone() =>
        new PythonModuleNode(
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Imports.Select(i => (PythonImportNode)i.Clone()).ToImmutableList(),
            TypeCheckingImports.Select(i => (PythonImportNode)i.Clone()).ToImmutableList(),
            FutureImports.Select(i => (PythonImportNode)i.Clone()).ToImmutableList(),
            ModuleDoc != null ? (CommentNode)ModuleDoc.Clone() : null,
            TrailingStatements.ToImmutableList(),
            Metadata.ToImmutableDictionary());
}
