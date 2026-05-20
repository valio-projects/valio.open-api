using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>
/// A Ruby constant declaration: <c>NAME = literal</c>. Used to render the
/// individual values of an IR enum (Ruby has no enum keyword, so a module
/// with frozen string constants is the idiomatic representation).
/// </summary>
public sealed class RubyConstantNode : DeclarationNode
{
    /// <summary>Pre-rendered Ruby literal (e.g. <c>'admin'</c>, <c>42</c>).</summary>
    public string ValueLiteral { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public RubyConstantNode(
        IdentifierNode name,
        string valueLiteral,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        ValueLiteral = valueLiteral ?? throw new ArgumentNullException(nameof(valueLiteral));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Rendered via the declaration dispatch in the renderer; there is no
        // dedicated visit method to avoid adding to IRubyAstVisitor surface.
        throw new NotSupportedException(
            "RubyConstantNode is rendered through RubyAstRenderer's declaration dispatch.");
    }

    public override AstNode Clone() =>
        new RubyConstantNode(
            (IdentifierNode)Name.Clone(),
            ValueLiteral,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
