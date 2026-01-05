using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел бинарного выражения
/// </summary>
public sealed class BinaryExpressionNode : ExpressionNode
{
    /// <summary>
    /// Левый операнд
    /// </summary>
    public ExpressionNode Left { get; }
    
    /// <summary>
    /// Оператор
    /// </summary>
    public BinaryOperator Operator { get; }
    
    /// <summary>
    /// Правый операнд
    /// </summary>
    public ExpressionNode Right { get; }
    
    public override AstNodeKind Kind => AstNodeKind.BinaryExpression;
    
    public override IReadOnlyList<AstNode> Children => new[] { Left, Right };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public BinaryExpressionNode(
        ExpressionNode left,
        BinaryOperator @operator,
        ExpressionNode right,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = @operator;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBinaryExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new BinaryExpressionNode(
            (ExpressionNode)Left.Clone(),
            Operator,
            (ExpressionNode)Right.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
