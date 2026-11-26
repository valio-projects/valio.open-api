using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>Propagates an error from a throwing Swift expression.</summary>
/// <param name="operand">The throwing expression.</param>
public sealed class SwiftTryExpressionNode(ExpressionNode operand) : ExpressionNode
{
    /// <summary>Gets the throwing expression.</summary>
    /// <value>The throwing expression.</value>
    public ExpressionNode Operand { get; } = operand;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Operand];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftTryExpression(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftTryExpressionNode((ExpressionNode)Operand.Clone());
}
