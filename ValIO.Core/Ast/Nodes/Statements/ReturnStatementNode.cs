using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел return statement
/// </summary>
public sealed class ReturnStatementNode : StatementNode
{
    /// <summary>
    /// Возвращаемое значение (null для return;)
    /// </summary>
    public ExpressionNode? Value { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (Documentation != null)
                children.Add(Documentation);
            if (Value != null)
                children.Add(Value);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ReturnStatementNode(
        ExpressionNode? value = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Value = value;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitReturnStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new ReturnStatementNode(
            Value != null ? (ExpressionNode)Value.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
