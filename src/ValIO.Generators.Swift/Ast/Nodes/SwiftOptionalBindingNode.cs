using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>Binds an unwrapped value in a Swift if or guard condition.</summary>
/// <param name="name">The binding identifier.</param>
/// <param name="value">The optional expression to unwrap.</param>
public sealed class SwiftOptionalBindingNode(IdentifierNode name, ExpressionNode value) : ExpressionNode
{
    /// <summary>Gets the binding identifier.</summary>
    /// <value>The binding identifier.</value>
    public IdentifierNode Name { get; } = name;

    /// <summary>Gets the optional expression to unwrap.</summary>
    /// <value>The optional expression to unwrap.</value>
    public ExpressionNode Value { get; } = value;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Name, Value];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftOptionalBinding(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftOptionalBindingNode((IdentifierNode)Name.Clone(), (ExpressionNode)Value.Clone());
}
