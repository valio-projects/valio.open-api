using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел выражения идентификатора
/// </summary>
public sealed class IdentifierExpressionNode : ExpressionNode
{
    /// <summary>
    /// Идентификатор
    /// </summary>
    public IdentifierNode Identifier { get; }
    
    public override AstNodeKind Kind => AstNodeKind.IdentifierExpression;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Identifier };
            if (Type != null)
                children.Add(Type);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public IdentifierExpressionNode(
        IdentifierNode identifier,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitIdentifierExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new IdentifierExpressionNode(
            (IdentifierNode)Identifier.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
