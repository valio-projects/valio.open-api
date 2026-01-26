using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел query key для TanStack Query
/// </summary>
public sealed class QueryKeyNode : ExpressionNode
{
    /// <summary>
    /// Части ключа (например, ['users', id])
    /// </summary>
    public IReadOnlyList<ExpressionNode> Parts { get; }
    
    public override AstNodeKind Kind => AstNodeKind.QueryKey;
    
    public override IReadOnlyList<AstNode> Children => Parts.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public QueryKeyNode(
        IReadOnlyList<ExpressionNode> parts,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        if (parts == null || parts.Count == 0)
            throw new ArgumentException("Query key must have at least one part", nameof(parts));
        
        Parts = parts.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitQueryKey(this);
        }
        return visitor.VisitExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new QueryKeyNode(
            Parts.Select(p => (ExpressionNode)p.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
