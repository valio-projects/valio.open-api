using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>
/// Узел исходного файла (TypeScript)
/// </summary>
public sealed class SourceFileNode : AstNode
{
    /// <summary>
    /// Импорты
    /// </summary>
    public IReadOnlyList<ImportNode> Imports { get; }
    
    /// <summary>
    /// Экспорты
    /// </summary>
    public IReadOnlyList<ExportNode> Exports { get; }
    
    /// <summary>
    /// Объявления
    /// </summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; }

    /// <summary>
    /// Необязательный комментарий-заголовок файла (JSDoc <c>@file</c>/<c>@module</c>/<c>@generated</c>),
    /// рендерится в самом верху файла перед импортами.
    /// </summary>
    public CommentNode? FileDocumentation { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            children.AddRange(Imports);
            children.AddRange(Exports);
            children.AddRange(Declarations);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public SourceFileNode(
        IReadOnlyList<ImportNode>? imports = null,
        IReadOnlyList<ExportNode>? exports = null,
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CommentNode? fileDocumentation = null)
        : base(metadata)
    {
        Imports = imports?.ToImmutableList() ?? ImmutableList<ImportNode>.Empty;
        Exports = exports?.ToImmutableList() ?? ImmutableList<ExportNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        FileDocumentation = fileDocumentation;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitSourceFile(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit SourceFileNode");
    }
    
    public override AstNode Clone()
    {
        return new SourceFileNode(
            Imports.Select(i => (ImportNode)i.Clone()).ToImmutableList(),
            Exports.Select(e => (ExportNode)e.Clone()).ToImmutableList(),
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary(),
            FileDocumentation != null ? (CommentNode)FileDocumentation.Clone() : null);
    }
}
