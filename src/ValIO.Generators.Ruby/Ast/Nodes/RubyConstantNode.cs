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
    /// <summary>Gets the expression that initializes the constant.</summary>
    /// <value>The initializer AST.</value>
    public ExpressionNode Initializer { get; }

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Initializer };
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    /// <summary>Declares a constant with a typed initializer.</summary>
    /// <param name="name">The constant identifier.</param>
    /// <param name="initializer">The value to assign to the constant.</param>
    /// <param name="documentation">The optional documentation.</param>
    /// <param name="metadata">Metadata retained by the declaration.</param>
    public RubyConstantNode(
        IdentifierNode name,
        ExpressionNode initializer,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Initializer = initializer;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Rendered via the declaration dispatch in the renderer; there is no
        // dedicated visit method to avoid adding to IRubyAstVisitor surface.
        throw new NotSupportedException(
            "RubyConstantNode is rendered through RubyAstRenderer's declaration dispatch.");
    }

    /// <inheritdoc />
    public override AstNode Clone() =>
        new RubyConstantNode(
            (IdentifierNode)Name.Clone(),
            (ExpressionNode)Initializer.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
