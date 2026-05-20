using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>
/// Ruby <c>class</c> declaration:
///   <c>class Name [&lt; Parent]</c>
///   <c>  body...</c>
///   <c>end</c>
/// </summary>
public sealed class RubyClassNode : DeclarationNode
{
    /// <summary>Optional parent class for inheritance.</summary>
    public string? ParentClass { get; }

    /// <summary>Member declarations (attributes + methods), in render order.</summary>
    public IReadOnlyList<DeclarationNode> Members { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Members);
            return children;
        }
    }

    public RubyClassNode(
        IdentifierNode name,
        string? parentClass = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        ParentClass = parentClass;
        Members = members?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRubyAstVisitor<TResult> rubyVisitor)
            return rubyVisitor.VisitRubyClass(this);
        throw new NotSupportedException("Visitor must implement IRubyAstVisitor<TResult> to visit RubyClassNode");
    }

    public override AstNode Clone() =>
        new RubyClassNode(
            (IdentifierNode)Name.Clone(),
            ParentClass,
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
