using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел useMutation хука TanStack Query
/// </summary>
public sealed class UseMutationNode : CallExpressionNode
{
    /// <summary>
    /// Mutation function
    /// </summary>
    public ExpressionNode MutationFn { get; }
    
    /// <summary>
    /// Опции mutation
    /// </summary>
    public MutationOptionsNode? Options { get; }
    
    public override AstNodeKind Kind => AstNodeKind.UseMutation;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { MutationFn };
            if (Options != null)
                children.Add(Options);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public UseMutationNode(
        ExpressionNode mutationFn,
        MutationOptionsNode? options = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(
            new IdentifierExpressionNode(new IdentifierNode("useMutation")),
            BuildArguments(mutationFn, options),
            type,
            metadata)
    {
        MutationFn = mutationFn ?? throw new ArgumentNullException(nameof(mutationFn));
        Options = options;
    }
    
    private static IReadOnlyList<ExpressionNode> BuildArguments(
        ExpressionNode mutationFn,
        MutationOptionsNode? options)
    {
        var args = new List<ExpressionNode>();
        
        // Если есть опции, создаем объект с mutationFn и опциями
        if (options != null && (options.Properties.Count > 0 || options.OnSuccess != null || options.OnError != null || options.OnSettled != null))
        {
            var configProperties = new List<PropertyAssignmentNode>
            {
                new(new IdentifierNode("mutationFn"), mutationFn)
            };
            
            if (options.OnSuccess != null)
                configProperties.Add(new PropertyAssignmentNode(new IdentifierNode("onSuccess"), options.OnSuccess));
            if (options.OnError != null)
                configProperties.Add(new PropertyAssignmentNode(new IdentifierNode("onError"), options.OnError));
            if (options.OnSettled != null)
                configProperties.Add(new PropertyAssignmentNode(new IdentifierNode("onSettled"), options.OnSettled));
            
            configProperties.AddRange(options.Properties);
            
            args.Add(new ObjectLiteralExpressionNode(configProperties));
        }
        else
        {
            // Если опций нет, просто передаем mutationFn
            args.Add(mutationFn);
        }
        
        return args.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitUseMutation(this);
        }
        return visitor.VisitCallExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new UseMutationNode(
            (ExpressionNode)MutationFn.Clone(),
            Options != null ? (MutationOptionsNode)Options.Clone() : null,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
