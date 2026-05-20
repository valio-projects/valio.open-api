using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>
/// A Ruby attribute accessor declaration rendered as
/// <c>attr_accessor :name</c>. The optional <see cref="Type"/> is used to
/// build a YARD documentation comment above the accessor.
/// </summary>
public sealed class RubyAttributeNode : DeclarationNode
{
    /// <summary>Optional type used for YARD documentation only.</summary>
    public TypeNode? Type { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Type != null) children.Add(Type);
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public RubyAttributeNode(
        IdentifierNode name,
        TypeNode? type = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Type = type;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRubyAstVisitor<TResult> rubyVisitor)
            return rubyVisitor.VisitRubyAttribute(this);
        throw new NotSupportedException("Visitor must implement IRubyAstVisitor<TResult> to visit RubyAttributeNode");
    }

    public override AstNode Clone() =>
        new RubyAttributeNode(
            (IdentifierNode)Name.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
