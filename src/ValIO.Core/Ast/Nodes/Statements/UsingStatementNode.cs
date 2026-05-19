using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел using statement (using (var x = ...) { ... })
/// </summary>
public sealed class UsingStatementNode : StatementNode
{
    /// <summary>
    /// Ресурс для использования (переменная или выражение)
    /// </summary>
    public ExpressionNode Resource { get; }
    
    /// <summary>
    /// Тело using statement
    /// </summary>
    public BlockNode Body { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Resource, Body };
            if (Documentation != null)
                children.Insert(0, Documentation);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public UsingStatementNode(
        ExpressionNode resource,
        BlockNode body,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitUsingStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new UsingStatementNode(
            (ExpressionNode)Resource.Clone(),
            (BlockNode)Body.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
