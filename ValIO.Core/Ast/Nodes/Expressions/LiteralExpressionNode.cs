using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел литерального выражения
/// </summary>
public sealed class LiteralExpressionNode : ExpressionNode
{
    /// <summary>
    /// Значение литерала
    /// </summary>
    public object Value { get; }
    
    /// <summary>
    /// Тип литерала
    /// </summary>
    public LiteralKind LiteralKind { get; }
    
    public override AstNodeKind Kind => AstNodeKind.LiteralExpression;
    
    public override IReadOnlyList<AstNode> Children => Type != null ? new[] { Type } : Array.Empty<AstNode>();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public LiteralExpressionNode(
        object value,
        LiteralKind kind,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        LiteralKind = kind;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitLiteralExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new LiteralExpressionNode(
            Value,
            LiteralKind,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
