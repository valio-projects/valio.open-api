using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>Represents a Swift array literal.</summary>
/// <param name="elements">The ordered array elements.</param>
public sealed class SwiftArrayExpressionNode(IReadOnlyList<ExpressionNode> elements) : ExpressionNode
{
    /// <summary>Gets the ordered array elements.</summary>
    /// <value>The ordered array elements.</value>
    public IReadOnlyList<ExpressionNode> Elements { get; } = elements.ToArray();

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => Elements;

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftArrayExpression(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftArrayExpressionNode(Elements.Select(element => (ExpressionNode)element.Clone()).ToArray());
}
