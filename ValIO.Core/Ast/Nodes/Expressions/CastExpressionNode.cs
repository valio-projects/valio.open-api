using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел выражения приведения типа (cast expression: (Type)expression)
/// </summary>
public sealed class CastExpressionNode : ExpressionNode
{
    /// <summary>
    /// Целевой тип приведения
    /// </summary>
    public TypeNode TargetType { get; }
    
    /// <summary>
    /// Выражение для приведения
    /// </summary>
    public ExpressionNode Expression { get; }
    
    /// <summary>
    /// Использовать оператор "as" вместо обычного cast (например, "x as Type" вместо "(Type)x")
    /// </summary>
    public bool UseAsOperator { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { TargetType, Expression };
            if (Type != null)
                children.Add(Type);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public CastExpressionNode(
        TypeNode targetType,
        ExpressionNode expression,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        bool useAsOperator = false)
        : base(type, metadata)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        UseAsOperator = useAsOperator;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitCastExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new CastExpressionNode(
            (TypeNode)TargetType.Clone(),
            (ExpressionNode)Expression.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary(),
            UseAsOperator);
    }
}
