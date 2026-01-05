using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел await выражения
/// </summary>
public sealed class AwaitExpressionNode : ExpressionNode
{
    /// <summary>
    /// Выражение для await
    /// </summary>
    public ExpressionNode Expression { get; }
    
    public override AstNodeKind Kind => AstNodeKind.AwaitExpression;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Expression };
            if (Type != null)
                children.Add(Type);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public AwaitExpressionNode(
        ExpressionNode expression,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitAwaitExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new AwaitExpressionNode(
            (ExpressionNode)Expression.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
