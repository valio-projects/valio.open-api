using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел useQuery хука TanStack Query
/// </summary>
public sealed class UseQueryNode : CallExpressionNode
{
    /// <summary>
    /// Query key
    /// </summary>
    public QueryKeyNode QueryKey { get; }
    
    /// <summary>
    /// Query function (обычно CallExpressionNode с методом клиента)
    /// </summary>
    public ExpressionNode QueryFn { get; }
    
    /// <summary>
    /// Опции query
    /// </summary>
    public QueryOptionsNode? Options { get; }
    
    public override AstNodeKind Kind => AstNodeKind.UseQuery;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { QueryKey, QueryFn };
            if (Options != null)
                children.Add(Options);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public UseQueryNode(
        QueryKeyNode queryKey,
        ExpressionNode queryFn,
        QueryOptionsNode? options = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(
            new IdentifierExpressionNode(new IdentifierNode("useQuery")),
            BuildArguments(queryKey, queryFn, options),
            type,
            metadata)
    {
        QueryKey = queryKey ?? throw new ArgumentNullException(nameof(queryKey));
        QueryFn = queryFn ?? throw new ArgumentNullException(nameof(queryFn));
        Options = options;
    }
    
    private static IReadOnlyList<ExpressionNode> BuildArguments(
        QueryKeyNode queryKey,
        ExpressionNode queryFn,
        QueryOptionsNode? options)
    {
        var args = new List<ExpressionNode>();
        
        // Создаем объект с queryKey и queryFn
        var configProperties = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("queryKey"), queryKey),
            new(new IdentifierNode("queryFn"), queryFn)
        };
        
        // Если есть опции, объединяем их
        if (options != null && options.Properties.Count > 0)
        {
            configProperties.AddRange(options.Properties);
        }
        
        args.Add(new ObjectLiteralExpressionNode(configProperties));
        
        return args.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitUseQuery(this);
        }
        return visitor.VisitCallExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new UseQueryNode(
            (QueryKeyNode)QueryKey.Clone(),
            (ExpressionNode)QueryFn.Clone(),
            Options != null ? (QueryOptionsNode)Options.Clone() : null,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
