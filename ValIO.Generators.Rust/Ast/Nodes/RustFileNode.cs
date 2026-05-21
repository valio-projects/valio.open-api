using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// A single Rust source file (.rs) with <c>use</c> directives and top-level declarations.
/// </summary>
public sealed class RustFileNode : AstNode
{
    public IReadOnlyList<string> Uses { get; }
    public IReadOnlyList<DeclarationNode> Declarations { get; }
    public CommentNode? ModuleDoc { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (ModuleDoc != null) children.Add(ModuleDoc);
            children.AddRange(Declarations);
            return children;
        }
    }

    public RustFileNode(
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyList<string>? uses = null,
        CommentNode? moduleDoc = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Uses = uses?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        ModuleDoc = moduleDoc;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustFile(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustFileNode");
    }

    public override AstNode Clone() =>
        new RustFileNode(
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Uses.ToImmutableList(),
            ModuleDoc != null ? (CommentNode)ModuleDoc.Clone() : null,
            Metadata.ToImmutableDictionary());
}
