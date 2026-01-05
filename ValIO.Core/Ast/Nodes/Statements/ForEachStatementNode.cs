using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел foreach statement
/// </summary>
public sealed class ForEachStatementNode : StatementNode
{
    /// <summary>
    /// Переменная итератора
    /// </summary>
    public IdentifierNode Variable { get; }
    
    /// <summary>
    /// Коллекция для итерации
    /// </summary>
    public ExpressionNode Collection { get; }
    
    /// <summary>
    /// Тело цикла
    /// </summary>
    public BlockNode Body { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ForEachStatement;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Variable, Collection, Body };
            if (Documentation != null)
                children.Insert(0, Documentation);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ForEachStatementNode(
        IdentifierNode variable,
        ExpressionNode collection,
        BlockNode body,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitForEachStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new ForEachStatementNode(
            (IdentifierNode)Variable.Clone(),
            (ExpressionNode)Collection.Clone(),
            (BlockNode)Body.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
