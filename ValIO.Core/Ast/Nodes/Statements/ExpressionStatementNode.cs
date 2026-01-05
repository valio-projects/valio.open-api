using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел expression statement (выражение как statement)
/// </summary>
public sealed class ExpressionStatementNode : StatementNode
{
    /// <summary>
    /// Выражение
    /// </summary>
    public ExpressionNode Expression { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ExpressionStatement;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Expression };
            if (Documentation != null)
                children.Insert(0, Documentation);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ExpressionStatementNode(
        ExpressionNode expression,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitExpressionStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new ExpressionStatementNode(
            (ExpressionNode)Expression.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
