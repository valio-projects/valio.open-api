using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// A single Dart source file (.dart) with imports and top-level declarations.
/// </summary>
public sealed class DartFileNode : AstNode
{
    public IReadOnlyList<DartImportNode> Imports { get; }
    public IReadOnlyList<DeclarationNode> Declarations { get; }
    public CommentNode? FileDoc { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (FileDoc != null) children.Add(FileDoc);
            children.AddRange(Imports);
            children.AddRange(Declarations);
            return children;
        }
    }

    public DartFileNode(
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyList<DartImportNode>? imports = null,
        CommentNode? fileDoc = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Imports = imports?.ToImmutableList() ?? ImmutableList<DartImportNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        FileDoc = fileDoc;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartFile(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartFileNode");
    }

    public override AstNode Clone() =>
        new DartFileNode(
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Imports.Select(i => (DartImportNode)i.Clone()).ToImmutableList(),
            FileDoc != null ? (CommentNode)FileDoc.Clone() : null,
            Metadata.ToImmutableDictionary());
}
