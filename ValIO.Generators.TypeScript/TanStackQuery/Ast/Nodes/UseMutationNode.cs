using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел useMutation хука TanStack Query
/// </summary>
public sealed class UseMutationNode : ExpressionNode
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
        : base(type, metadata)
    {
        MutationFn = mutationFn ?? throw new ArgumentNullException(nameof(mutationFn));
        Options = options;
    }
    
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitUseMutation(this);
        }
        return visitor.VisitExpression(this);
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
