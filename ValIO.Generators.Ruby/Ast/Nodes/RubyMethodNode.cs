using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>
/// Ruby method declaration:
///   <c>def name(params)</c>
///   <c>  body...</c>
///   <c>end</c>
/// Body lines are pre-rendered strings: Ruby's "AST" is method name +
/// parameter names; the body is intentionally string-typed to keep the AST
/// minimal (this is allowed because of the AST-first carve-out for body
/// strings produced inside builders).
/// </summary>
public sealed class RubyMethodNode : DeclarationNode
{
    /// <summary>Parameter names (positional). Pre-rendered (already includes any default value clause).</summary>
    public IReadOnlyList<string> Parameters { get; }

    /// <summary>Method body lines (pre-rendered, without outer indent).</summary>
    public IReadOnlyList<string> Body { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public RubyMethodNode(
        IdentifierNode name,
        IReadOnlyList<string>? parameters = null,
        IReadOnlyList<string>? body = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Body = body?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRubyAstVisitor<TResult> rubyVisitor)
            return rubyVisitor.VisitRubyMethod(this);
        throw new NotSupportedException("Visitor must implement IRubyAstVisitor<TResult> to visit RubyMethodNode");
    }

    public override AstNode Clone() =>
        new RubyMethodNode(
            (IdentifierNode)Name.Clone(),
            Parameters.ToImmutableList(),
            Body.ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
