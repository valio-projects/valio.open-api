using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел опций для useQuery
/// </summary>
public sealed class QueryOptionsNode : ObjectLiteralExpressionNode
{
    /// <summary>
    /// Enabled опция
    /// </summary>
    public ExpressionNode? Enabled { get; }
    
    /// <summary>
    /// Stale time опция
    /// </summary>
    public ExpressionNode? StaleTime { get; }
    
    /// <summary>
    /// Cache time опция (gcTime в v5)
    /// </summary>
    public ExpressionNode? CacheTime { get; }
    
    /// <summary>
    /// Refetch on mount опция
    /// </summary>
    public ExpressionNode? RefetchOnMount { get; }
    
    /// <summary>
    /// Refetch on window focus опция
    /// </summary>
    public ExpressionNode? RefetchOnWindowFocus { get; }
    
    /// <summary>
    /// Retry опция
    /// </summary>
    public ExpressionNode? Retry { get; }
    
    public override AstNodeKind Kind => AstNodeKind.QueryOptions;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (Enabled != null) children.Add(Enabled);
            if (StaleTime != null) children.Add(StaleTime);
            if (CacheTime != null) children.Add(CacheTime);
            if (RefetchOnMount != null) children.Add(RefetchOnMount);
            if (RefetchOnWindowFocus != null) children.Add(RefetchOnWindowFocus);
            if (Retry != null) children.Add(Retry);
            children.AddRange(Properties);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public QueryOptionsNode(
        ExpressionNode? enabled = null,
        ExpressionNode? staleTime = null,
        ExpressionNode? cacheTime = null,
        ExpressionNode? refetchOnMount = null,
        ExpressionNode? refetchOnWindowFocus = null,
        ExpressionNode? retry = null,
        IReadOnlyList<PropertyAssignmentNode>? properties = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(properties ?? ImmutableList<PropertyAssignmentNode>.Empty, type, metadata)
    {
        Enabled = enabled;
        StaleTime = staleTime;
        CacheTime = cacheTime;
        RefetchOnMount = refetchOnMount;
        RefetchOnWindowFocus = refetchOnWindowFocus;
        Retry = retry;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitQueryOptions(this);
        }
        return visitor.VisitObjectLiteralExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new QueryOptionsNode(
            Enabled != null ? (ExpressionNode)Enabled.Clone() : null,
            StaleTime != null ? (ExpressionNode)StaleTime.Clone() : null,
            CacheTime != null ? (ExpressionNode)CacheTime.Clone() : null,
            RefetchOnMount != null ? (ExpressionNode)RefetchOnMount.Clone() : null,
            RefetchOnWindowFocus != null ? (ExpressionNode)RefetchOnWindowFocus.Clone() : null,
            Retry != null ? (ExpressionNode)Retry.Clone() : null,
            Properties.Select(p => (PropertyAssignmentNode)p.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
