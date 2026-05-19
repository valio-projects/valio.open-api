using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел if statement
/// </summary>
public sealed class IfStatementNode : StatementNode
{
    /// <summary>
    /// Условие
    /// </summary>
    public ExpressionNode Condition { get; }
    
    /// <summary>
    /// Then блок
    /// </summary>
    public BlockNode ThenBlock { get; }
    
    /// <summary>
    /// Else блок (опционально)
    /// </summary>
    public BlockNode? ElseBlock { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Condition, ThenBlock };
            if (Documentation != null)
                children.Insert(0, Documentation);
            if (ElseBlock != null)
                children.Add(ElseBlock);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public IfStatementNode(
        ExpressionNode condition,
        BlockNode thenBlock,
        BlockNode? elseBlock = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        ThenBlock = thenBlock ?? throw new ArgumentNullException(nameof(thenBlock));
        ElseBlock = elseBlock;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitIfStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new IfStatementNode(
            (ExpressionNode)Condition.Clone(),
            (BlockNode)ThenBlock.Clone(),
            ElseBlock != null ? (BlockNode)ElseBlock.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
