using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел throw statement
/// </summary>
public sealed class ThrowStatementNode : StatementNode
{
    /// <summary>
    /// Выражение для выбрасывания (обычно new Exception(...))
    /// </summary>
    public ExpressionNode? Expression { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ThrowStatement;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (Expression != null)
                children.Add(Expression);
            if (Documentation != null)
                children.Insert(0, Documentation);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ThrowStatementNode(
        ExpressionNode? expression = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Expression = expression;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitThrowStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new ThrowStatementNode(
            Expression != null ? (ExpressionNode)Expression.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
