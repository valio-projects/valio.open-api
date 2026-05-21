using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// Single Rust enum variant: <c>Active</c> or <c>NewType(Inner)</c>.
/// </summary>
public sealed class RustEnumVariantNode : DeclarationNode
{
    /// <summary>Optional payload type (for tuple variants).</summary>
    public TypeNode? Payload { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            if (Payload != null) children.Add(Payload);
            return children;
        }
    }

    public RustEnumVariantNode(
        IdentifierNode name,
        TypeNode? payload = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Payload = payload;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustEnumVariant(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustEnumVariantNode");
    }

    public override AstNode Clone() =>
        new RustEnumVariantNode(
            (IdentifierNode)Name.Clone(),
            Payload != null ? (TypeNode)Payload.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
