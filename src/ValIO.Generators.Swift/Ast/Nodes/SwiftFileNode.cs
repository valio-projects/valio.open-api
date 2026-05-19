using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// A single Swift source file (.swift) with imports and top-level declarations.
/// </summary>
public sealed class SwiftFileNode : AstNode
{
    public IReadOnlyList<SwiftImportNode> Imports { get; }
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

    public SwiftFileNode(
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyList<SwiftImportNode>? imports = null,
        CommentNode? fileDoc = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Imports = imports?.ToImmutableList() ?? ImmutableList<SwiftImportNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        FileDoc = fileDoc;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftFile(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftFileNode");
    }

    public override AstNode Clone() =>
        new SwiftFileNode(
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Imports.Select(i => (SwiftImportNode)i.Clone()).ToImmutableList(),
            FileDoc != null ? (CommentNode)FileDoc.Clone() : null,
            Metadata.ToImmutableDictionary());
}
