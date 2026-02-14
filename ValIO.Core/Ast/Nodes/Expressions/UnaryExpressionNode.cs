using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел унарного выражения (например, *x, &amp;x, !x)
/// </summary>
public sealed class UnaryExpressionNode : ExpressionNode
{
    public UnaryOperator Operator { get; }
    public ExpressionNode Operand { get; }

    public override AstNodeKind Kind => AstNodeKind.UnaryExpression;

    public override IReadOnlyList<AstNode> Children => new[] { Operand };

    public UnaryExpressionNode(
        UnaryOperator @operator,
        ExpressionNode operand,
        TypeNode? type = null)
        : base(type)
    {
        Operator = @operator;
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitUnaryExpression(this);
    }

    public override AstNode Clone()
    {
        return new UnaryExpressionNode(Operator, (ExpressionNode)Operand.Clone(), Type != null ? (TypeNode)Type.Clone() : null);
    }
}
